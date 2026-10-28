// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;
using Buildvana.Tool.Build;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Infrastructure.DependencyInjection;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Hosts.Forgejo;
using Buildvana.Tool.Services.Hosts.Gitea;
using Buildvana.Tool.Services.Hosts.GitHub;
using Buildvana.Tool.Services.Hosts.GitLab;
using Buildvana.Tool.Services.Platforms;
using Buildvana.Tool.Services.Release;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

/// <summary>
/// Runs <c>bv release</c> end to end over a real Git repository in a temporary directory, with only the
/// process and platform boundaries faked: child processes (<see cref="FakeProcessRunner"/>), hooks
/// (<see cref="FakeFileBasedAppRunner"/>), the CI platform (<see cref="FakeCIPlatform"/>), the Git host
/// (<see cref="RecordingHost"/>), and the release remote (<see cref="FakeReleaseRemoteResolver"/>, naming
/// the bare remote of the repository). Everything else — the service graph, the versioning, the changelog,
/// the commits, the tag, and the push — is the real thing.
/// </summary>
/// <remarks>
/// <para>The harness anchors the process's current directory to the repository, exactly like a real run does,
/// and sets the environment variables the configured NuGet feed and the host read their secrets from. All of
/// them are process-wide, so tests using this harness must be marked <c>[NotInParallel]</c>. All of them are
/// restored on disposal.</para>
/// </remarks>
internal sealed class ReleaseHarness : IDisposable
{
    /// <summary>
    /// The version the repository's in-tree references point at before the release, i.e. the one the
    /// self-reference update rewrites.
    /// </summary>
    public const string PreviousVersion = "2.3.0-preview";

    /// <summary>
    /// The directory the declared API files live in, relative to the repository's root. A subdirectory,
    /// like in a real repository, where the files belong to a project rather than to the repository.
    /// </summary>
    public const string ApiFilesDirectory = "src/Test.Lib";

    private const string ApiKeyEnvVar = "BV_TEST_NUGET_API_KEY";
    private const string RemoteName = "origin";
    private const string BranchName = "release";
    private const string Configuration = "Release";

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly HashSet<string> StepNames = [.. Enum.GetNames<BuildStep>()];

    private readonly ReleaseHarnessOptions _options;
    private readonly TestConsole _console = new();
    private readonly List<ReleaseEvent> _events = [];
    private readonly ServiceProvider _services;
    private readonly string _originalCurrentDirectory = Environment.CurrentDirectory;
    private readonly string? _originalApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
    private readonly string? _originalToken;

    private IReadOnlyList<string> _commandOptions = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseHarness"/> class, populating the repository and
    /// composing the service graph.
    /// </summary>
    /// <param name="options">The harness options, or <see langword="null"/> for the defaults.</param>
    public ReleaseHarness(ReleaseHarnessOptions? options = null)
    {
        _options = options ?? new ReleaseHarnessOptions();
        ProcessRunner = new FakeProcessRunner { OnRun = RunDotNet };
        AppRunner.OnRun = (_, _, _) => {
            Record("hook");
            HookBehavior?.Invoke();
        };

        Environment.SetEnvironmentVariable(ApiKeyEnvVar, "test-api-key");
        _originalToken = Environment.GetEnvironmentVariable(_options.HostTokenEnv);
        Environment.SetEnvironmentVariable(_options.HostTokenEnv, _options.PushSecret);
        Repo = new TempGitRepo();
        PopulateRepository();
        Repo.CommitAll("Initial commit");
        Repo.CheckoutNewBranch(BranchName);
        _ = Repo.AddBareRemote();

        // Written after the initial commit, so that it stays untracked and no commit carries the version line.
        if (!_options.CommitVersionFile)
        {
            WriteVersionFile();
        }

        _services = BuildServiceProvider();
    }

    /// <summary>
    /// Gets the IDs of the packages the faked <c>pack</c> produces, one per kind of file the self-reference
    /// update rewrites: an MSBuild SDK (<c>global.json</c>), a .NET tool (<c>dotnet-tools.json</c>),
    /// and an ordinary package (<c>Directory.Packages.props</c>).
    /// </summary>
    public static IReadOnlyList<string> ProducedPackageIds { get; } = ["Test.Sdk", "Test.Tool", "Test.Lib"];

    /// <summary>
    /// Gets the repository the release runs on.
    /// </summary>
    public TempGitRepo Repo { get; }

    /// <summary>
    /// Gets the reporter the release reports to.
    /// </summary>
    public CaptureReporter Reporter { get; } = new();

    /// <summary>
    /// Gets the fake standing in for every <c>dotnet</c> child process.
    /// </summary>
    public FakeProcessRunner ProcessRunner { get; }

    /// <summary>
    /// Gets the fake standing in for the hook's file-based app.
    /// </summary>
    public FakeFileBasedAppRunner AppRunner { get; } = new();

    /// <summary>
    /// Gets the messages the release recorded at <see cref="MessageLevel.Notice"/> level, in order: the
    /// record of what the release did, which is what a run at the default verbosity shows.
    /// </summary>
    public IEnumerable<string> Notices
        => Reporter.Messages.Where(x => x.Level == MessageLevel.Notice).Select(x => x.Message);

    /// <summary>
    /// Gets the observable steps of the release, in order.
    /// </summary>
    public IReadOnlyList<ReleaseEvent> Events => _events;

    /// <summary>
    /// Gets the build pipeline steps the release ran, in order. <see cref="BuildPipeline"/> begins a reporter
    /// activity named after each step, so the list also holds the steps that invoke no child process:
    /// <see cref="BuildStep.Clean"/>, and <see cref="BuildStep.Test"/> over a solution without test projects.
    /// <see cref="Events"/> sees neither.
    /// </summary>
    public IReadOnlyList<BuildStep> PipelineSteps
        => [.. Reporter.ActivityTitles.Where(StepNames.Contains).Select(Enum.Parse<BuildStep>)];

    /// <summary>
    /// Gets the full path of the artifacts directory the release builds into.
    /// </summary>
    public string ArtifactsPath => Path.Combine(Repo.RootPath, "artifacts", Configuration);

    /// <summary>
    /// Gets the Git host the release drives.
    /// </summary>
    /// <exception cref="InvalidCastException">
    /// The harness runs on an unknown host (<see cref="ReleaseHarnessOptions.UnknownHost"/>).
    /// </exception>
    public RecordingHost Host => (RecordingHost)_services.GetRequiredService<GitHost>();

    /// <summary>
    /// Gets the release publisher the release created, or <see langword="null"/> if no release began.
    /// </summary>
    public RecordingReleasePublisher? Publisher => Host.Publisher;

    /// <summary>
    /// Gets or sets a callback simulating what the hook does to the working tree. Only ever invoked when
    /// the repository has a hook file (<see cref="ReleaseHarnessOptions.WithHook"/>).
    /// </summary>
    public Action? HookBehavior { get; set; }

    /// <summary>
    /// Runs the <c>release</c> command.
    /// </summary>
    /// <param name="options">The command-line option tokens to run it with.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is the
    /// command's exit code.</returns>
    public Task<int> RunAsync(params string[] options)
    {
        _commandOptions = options;
        return _services.GetRequiredService<ReleaseCommand>().ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Begins a release over the service graph of the harness, the way the <c>release</c> command does, with
    /// no step outputs.
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is the release.</returns>
    public Task<ServerRelease> BeginReleaseAsync()
        => ServerRelease.BeginAsync(
            _services.GetRequiredService<IReporter>(),
            _services.GetRequiredService<IGitService>(),
            _services.GetRequiredService<VersionService>(),
            _services.GetRequiredService<GitHost>().CreateReleasePublisher(),
            RemoteName,
            stepOutputs: null);

    /// <summary>
    /// Computes the version of the repository as it now stands, the way a build of it would: this is what
    /// the pack step stamps on the packages, and what a later build of the released commit produces.
    /// </summary>
    /// <returns>The version, in full semantic version form.</returns>
    public string ComputeVersion()
    {
        var config = new BuildvanaConfig { Versioning = new VersioningConfig { PrereleaseTag = "preview" } };
        var calculator = new VersionCalculator(
            new FixedHomeDirectoryProvider(Repo.RootPath),
            new VersioningSettings(config),
            new GitHeightCalculator(VersionFile.FileName));
        return calculator.Calculate().SemVer;
    }

    /// <summary>
    /// Reads a file of the repository.
    /// </summary>
    /// <param name="relativePath">The path of the file, relative to the repository's root.</param>
    /// <returns>The content of the file.</returns>
    public string ReadFile(string relativePath) => File.ReadAllText(Path.Combine(Repo.RootPath, relativePath));

    /// <summary>
    /// Writes a file of the repository, creating its directory if needed.
    /// </summary>
    /// <param name="relativePath">The path of the file, relative to the repository's root.</param>
    /// <param name="content">The content of the file.</param>
    public void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(Repo.RootPath, relativePath);
        var directory = Path.GetDirectoryName(path);
        Debug.Assert(directory is not null, "GetDirectoryName returns null only for a root, and a file path is not one.");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _services.Dispose();
        _console.Dispose();

        // The current directory has to leave the repository before the repository can be deleted.
        Environment.CurrentDirectory = _originalCurrentDirectory;
        Environment.SetEnvironmentVariable(ApiKeyEnvVar, _originalApiKey);
        Environment.SetEnvironmentVariable(_options.HostTokenEnv, _originalToken);
        Repo.Dispose();
    }

    private static ProcessResult Success(string executable, IReadOnlyList<string> args)
        => new($"{executable} {string.Join(' ', args)}", 0, string.Empty, string.Empty, TimeSpan.Zero);

    private static GitUrlInfo ParseRepositoryUrl(IServiceProvider services)
        => GitHost.ParseRepositoryUrl(services.GetRequiredService<BuildvanaConfig>().Repository);

    private ServiceProvider BuildServiceProvider()
        => new ServiceCollection()
            .AddSingleton<IAnsiConsole>(_console)
            .AddSingleton<IReporter>(Reporter)
            .AddSingleton(new GlobalSettings(null, false, false, true, true, true, false))
            .AddSingleton(_ => new CommandParameters(_commandOptions, [], []))
            .AddSingleton<IHomeDirectoryProvider>(
                _ => new AnchoringHomeDirectoryProvider(new FixedHomeDirectoryProvider(Repo.RootPath)))
            .AddBvServices()

            // The boundaries, faked. Registered last, so that they win over what AddBvServices registers.
            .AddSingleton<IProcessRunner>(ProcessRunner)
            .AddSingleton<IFileBasedAppRunner>(AppRunner)
            .AddSingleton<CIPlatform>(new FakeCIPlatform {
                RunsOnCI = _options.IsCI,
                Identity = _options.WithBotIdentity ? new("Buildvana Test Bot", "bot@buildvana.invalid") : null,
            })
            .AddSingleton<ReleaseRemoteResolver>(new FakeReleaseRemoteResolver(RemoteName))
            .AddSingleton(CreateHost)
            .BuildServiceProvider();

    private GitHost CreateHost(IServiceProvider services)
    {
        if (_options.UnknownHost)
        {
            return new UnknownHost(ParseRepositoryUrl(services), Reporter);
        }

        if (_options.GitHubApi is { } gitHubApi)
        {
            return new GitHubHost(services, ParseRepositoryUrl(services), gitHubApi);
        }

        if (_options.GitLabApi is { } gitLabApi)
        {
            return new GitLabHost(services, ParseRepositoryUrl(services), gitLabApi);
        }

        if (_options.GiteaApi is { } giteaApi)
        {
            return new GiteaHost(services, ParseRepositoryUrl(services), giteaApi);
        }

        if (_options.ForgejoApi is { } forgejoApi)
        {
            return new ForgejoHost(services, ParseRepositoryUrl(services), forgejoApi);
        }

        return new RecordingHost {
            TokenVariable = _options.HostTokenEnv,
            PushUser = _options.PushUser,
            OnPublishing = () => {
                Record("publish");
                _options.OnPublishing?.Invoke();
            },
        };
    }

    // Every dotnet invocation is a no-op that succeeds, except `pack`, which leaves behind the artifacts
    // a real one would: the release reads them back to discover the produced packages and gather assets.
    private ProcessResult RunDotNet(string executable, IReadOnlyList<string> args)
    {
        var verb = args.FirstOrDefault(x => !x.StartsWith('-')) ?? "?";
        if (verb == "nuget")
        {
            verb = "nuget-push";
        }

        Record(verb);
        if (verb == "pack")
        {
            WriteArtifacts();
        }

        return Success(executable, args);
    }

    private void WriteArtifacts()
    {
        // The version is computed from the repository, not read off the command that is driving this pack:
        // a real `dotnet pack` computes it independently too, and a release that publishes a version its
        // own artifacts do not carry is precisely the divergence these tests have to be able to see.
        var version = ComputeVersion();
        _ = Directory.CreateDirectory(ArtifactsPath);
        foreach (var id in ProducedPackageIds)
        {
            WriteArtifact($"{id}.{version}.nupkg");
        }

        // Symbols travel with the package they belong to; only one of the three has any.
        WriteArtifact($"{ProducedPackageIds[2]}.{version}.snupkg");

        // A release asset list, as Buildvana SDK writes it: one valid line, one malformed line, and one
        // line naming a file that is not there. The last two are reported and skipped.
        var setupPath = WriteArtifact("setup.exe");
        var assetList = $"""
            {setupPath}	application/vnd.microsoft.portable-executable	Setup program
            this line has no tabs at all
            {Path.Combine(ArtifactsPath, "not-there.bin")}	application/octet-stream	Missing asset

            """.ReplaceLineEndings("\n");
        File.WriteAllText(Path.Combine(ArtifactsPath, "Test.assets.txt"), assetList);
    }

    private string WriteArtifact(string name)
    {
        var path = Path.Combine(ArtifactsPath, name);
        File.WriteAllText(path, $"fake content of {name}");
        return path;
    }

    private void Record(string name)
    {
        var commits = Repo.GetCommits(1);
        _events.Add(new(name, commits.Count > 0 ? commits[0].Message : string.Empty, Repo.GetRemoteTipSha()));
    }

    private void PopulateRepository()
    {
        Repo.WriteFile(".gitignore", "artifacts/\n" + WellKnownPaths.ScratchDirectory + "/\n");
        if (_options.CommitVersionFile)
        {
            WriteVersionFile();
        }

        Repo.WriteFile("Test.slnx", "<Solution />\n");
        Repo.WriteFile("buildvana.json", BuildConfiguration());
        WriteSelfReferenceTargets();
        if (_options.Changelog is { } changelog)
        {
            Repo.WriteFile("CHANGELOG.md", changelog);
        }

        if (_options.UnshippedPublicApi is { } unshippedPublic)
        {
            WriteFile($"{ApiFilesDirectory}/PublicAPI.Unshipped.txt", unshippedPublic);
            WriteFile($"{ApiFilesDirectory}/PublicAPI.Shipped.txt", "#nullable enable\n");
        }

        if (_options.UnshippedInternalApi is { } unshippedInternal)
        {
            WriteFile($"{ApiFilesDirectory}/InternalAPI.Unshipped.txt", unshippedInternal);
            WriteFile($"{ApiFilesDirectory}/InternalAPI.Shipped.txt", "#nullable enable\n");
        }

        if (_options.WithHook)
        {
            WriteFile(WellKnownPaths.GetHookFile("release", "post-release"), "// never executed: the app runner is faked\n");
        }
    }

    private void WriteVersionFile() => Repo.WriteFile("VERSION", _options.VersionSpec + "\n");

    // The three files the self-reference update rewrites, each carrying a reference to one of the packages
    // this release produces, at the version released before this one. Only the first
    // ReleaseHarnessOptions.SelfReferenceTargets of them are written, so that a test can have the update
    // find any number of them, none included.
    private void WriteSelfReferenceTargets()
    {
        if (_options.SelfReferenceTargets < 1)
        {
            return;
        }

        var globalJson = new JsonObject {
            ["msbuild-sdks"] = new JsonObject { [ProducedPackageIds[0]] = PreviousVersion },
        };
        WriteFile("global.json", globalJson.ToJsonString(IndentedJson));
        if (_options.SelfReferenceTargets < 2)
        {
            return;
        }

        var toolManifest = new JsonObject {
            ["version"] = 1,
            ["isRoot"] = true,
            ["tools"] = new JsonObject {
                [ProducedPackageIds[1]] = new JsonObject {
                    ["version"] = PreviousVersion,
                    ["commands"] = new JsonArray("test-tool"),
                },
            },
        };
        WriteFile("dotnet-tools.json", toolManifest.ToJsonString(IndentedJson));
        if (_options.SelfReferenceTargets < 3)
        {
            return;
        }

        var packageVersions = $"""
            <Project>
              <ItemGroup>
                <PackageVersion Include="{ProducedPackageIds[2]}" Version="{PreviousVersion}" />
              </ItemGroup>
            </Project>

            """;
        WriteFile("Directory.Packages.props", packageVersions);
    }

    private string BuildConfiguration()
    {
        var release = new JsonObject {
            ["branches"] = new JsonArray($"^{BranchName}$"),
            ["checkPublicApi"] = _options.CheckPublicApi,
            ["changelogUpdates"] = _options.ChangelogUpdates,
            ["dogfood"] = _options.Dogfood,
            ["allowUnknownHost"] = _options.AllowUnknownHost,
        };
        if (_options.EmptyChangelog is { } emptyChangelog)
        {
            release["emptyChangelog"] = emptyChangelog;
        }

        var feed = new JsonObject {
            ["source"] = "https://nuget.example.invalid/v3/index.json",
            ["apiKeyEnv"] = ApiKeyEnvVar,
        };
        var root = new JsonObject {
            ["release"] = release,
            ["versioning"] = new JsonObject { ["prereleaseTag"] = "preview" },
            ["dotnet"] = new JsonObject { ["configuration"] = Configuration },
            ["nuget"] = new JsonObject { ["feeds"] = new JsonObject { ["release"] = feed } },
            ["hosts"] = new JsonObject {
                ["github"] = new JsonObject { ["tokenEnv"] = _options.HostTokenEnv },
                ["gitlab"] = new JsonObject { ["tokenEnv"] = _options.HostTokenEnv },
                ["gitea"] = new JsonObject { ["tokenEnv"] = _options.HostTokenEnv },
                ["forgejo"] = new JsonObject { ["tokenEnv"] = _options.HostTokenEnv },
            },
        };
        if (_options.RepositoryUrl is { } repositoryUrl)
        {
            root["repository"] = new JsonObject { ["url"] = repositoryUrl };
        }

        if (_options.ConfiguredIdentity is { } identity)
        {
            root["git"] = new JsonObject {
                ["identity"] = new JsonObject { ["name"] = identity.Name, ["email"] = identity.Email },
            };
        }

        return root.ToJsonString(IndentedJson);
    }
}
