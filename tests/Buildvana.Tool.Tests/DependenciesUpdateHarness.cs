// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure.DependencyInjection;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Subcommands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

/// <summary>
/// Runs <c>bv dependencies update</c> end to end over a temporary home directory, with only the process
/// boundaries faked: child processes (<see cref="FakeProcessRunner"/>), the hook
/// (<see cref="FakeFileBasedAppRunner"/>), the restore the override lifecycle would run, and the two version
/// sources. Everything else — the service graph, the discovery, the resolution, and the writers — is the real
/// thing.
/// </summary>
/// <remarks>
/// <para>Every run leaves the packages scope out, so nothing here spawns MSBuild. The solution factory
/// throws to make that a fact rather than a hope, and <see cref="SolutionWasAsked"/> says whether anything
/// reached for it.</para>
/// <para>Nothing here touches process-wide state, so tests using this harness need no
/// <c>[NotInParallel]</c>.</para>
/// </remarks>
internal sealed class DependenciesUpdateHarness : IDisposable
{
    /// <summary>The id of the project SDK the repository pins in <c>global.json</c>.</summary>
    public const string SdkId = "Contoso.Sdk";

    /// <summary>The id of the local tool the repository pins in its manifest.</summary>
    public const string ToolId = "ngbv";

    /// <summary>The .NET SDK version the repository states before the run.</summary>
    public const string OldNetSdkVersion = "10.0.100";

    /// <summary>The .NET SDK version a run under the default policy moves the baseline to.</summary>
    public const string NewNetSdkVersion = "10.0.201";

    private const string GlobalJsonName = "global.json";
    private const string ToolManifestName = ".config/dotnet-tools.json";

    private const string GlobalJson = """
                                      {
                                        "sdk": {
                                          "version": "10.0.100",
                                          "allowPrerelease": false
                                        },
                                        "msbuild-sdks": {
                                          "Contoso.Sdk": "1.0.0"
                                        }
                                      }
                                      """;

    private const string ToolManifest = """
                                        {
                                          "version": 1,
                                          "isRoot": true,
                                          "tools": {
                                            "ngbv": {
                                              "version": "0.5.1",
                                              "commands": [ "ngbv" ]
                                            }
                                          }
                                        }
                                        """;

    private readonly TempHome _home = new();
    private readonly TestConsole _console = new();
    private readonly List<DependencyUpdateStep> _steps = [];
    private readonly ThrowingSolutionContextFactory _solutions = new();
    private readonly ServiceProvider _services;

    private IReadOnlyList<string> _options = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DependenciesUpdateHarness"/> class, populating the home
    /// directory and composing the service graph.
    /// </summary>
    /// <param name="hookExitCode">The exit code the hook answers with.</param>
    /// <param name="upToDate">Whether the repository already states everything its policies allow.</param>
    public DependenciesUpdateHarness(int hookExitCode = 0, bool upToDate = false)
    {
        ProcessRunner = new FakeProcessRunner { OnRun = RunDotNet };
        AppRunner = new FakeFileBasedAppRunner { ExitCode = hookExitCode, OnRun = (_, _, _) => Record("hook") };
        NetSdkReleases = upToDate
            ? new FakeNetSdkReleaseSource().Knows(isLts: true, OldNetSdkVersion)
            : new FakeNetSdkReleaseSource().Knows(isLts: true, OldNetSdkVersion, NewNetSdkVersion);

        PackageVersions = upToDate
            ? new FakePackageVersionSource().Knows(SdkId, ["1.0.0"]).Knows(ToolId, ["0.5.1"])
            : new FakePackageVersionSource().Knows(SdkId, ["1.0.0", "1.1.0"]).Knows(ToolId, ["0.5.1", "0.6.0"]);

        _home.WriteFile(GlobalJsonName, GlobalJson);
        _home.WriteFile(ToolManifestName, ToolManifest);
        _home.WriteFile(
            WellKnownPaths.GetHookFile(PostUpdateHookArgs.Context, PostUpdateHookArgs.Event),
            "// never executed: the app runner is faked\n");

        _services = BuildServiceProvider();
    }

    /// <summary>Gets the reporter the run reports to.</summary>
    public CaptureReporter Reporter { get; } = new();

    /// <summary>Gets the fake standing in for every <c>dotnet</c> child process.</summary>
    public FakeProcessRunner ProcessRunner { get; }

    /// <summary>Gets the fake standing in for the hook's file-based app.</summary>
    public FakeFileBasedAppRunner AppRunner { get; }

    /// <summary>Gets the source scripted with the .NET SDK releases the run sees.</summary>
    public FakeNetSdkReleaseSource NetSdkReleases { get; }

    /// <summary>Gets the source scripted with the package versions the run sees.</summary>
    public FakePackageVersionSource PackageVersions { get; }

    /// <summary>Gets the observable steps of the run, in order.</summary>
    public IReadOnlyList<DependencyUpdateStep> Steps => _steps;

    /// <summary>Gets a value indicating whether anything reached for the solution.</summary>
    public bool SolutionWasAsked => _solutions.WasAsked;

    /// <summary>Gets the content of <c>global.json</c> as it now stands.</summary>
    public string GlobalJsonNow => _home.ReadFile(GlobalJsonName);

    /// <summary>
    /// Runs the <c>dependencies update</c> command, with the packages scope left out.
    /// </summary>
    /// <param name="options">The command-line option tokens to run it with.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is the
    /// command's exit code.</returns>
    public Task<int> RunAsync(params string[] options)
    {
        _options = ["--no-packages", .. options];
        return _services.GetRequiredService<DependenciesUpdateCommand>().ExecuteAsync(CancellationToken.None);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _services.Dispose();
        _console.Dispose();
        _home.Dispose();
    }

    private ServiceProvider BuildServiceProvider()
        => new ServiceCollection()
            .AddSingleton<IAnsiConsole>(_console)
            .AddSingleton<IReporter>(Reporter)
            .AddSingleton(new GlobalSettings(null, false, false, true, true, true, false))
            .AddSingleton(_ => new CommandParameters(_options, [], []))
            .AddSingleton<IHomeDirectoryProvider>(_home.Provider)
            .AddBvServices()

            // The boundaries, faked. Registered last, so that they win over what AddBvServices registers.
            .AddSingleton<IProcessRunner>(ProcessRunner)
            .AddSingleton<IFileBasedAppRunner>(AppRunner)
            .AddSingleton<IDependencyRestorer>(new FakeDependencyRestorer())
            .AddSingleton<IPackageVersionSource>(PackageVersions)
            .AddSingleton<INetSdkReleaseSource>(NetSdkReleases)
            .AddSingleton<ISolutionContextFactory>(_solutions)
            .BuildServiceProvider();

    // The one child process a dependencies update run spawns is `dotnet tool update`, which writes the
    // manifest and installs the tool. Neither happens here: what the step is for is its place in the order.
    private ProcessResult RunDotNet(string executable, IReadOnlyList<string> args)
    {
        Record("tool");
        return new ProcessResult($"{executable} {string.Join(' ', args)}", 0, string.Empty, string.Empty, TimeSpan.Zero);
    }

    private void Record(string name) => _steps.Add(new(name, _home.ReadFile(GlobalJsonName)));
}
