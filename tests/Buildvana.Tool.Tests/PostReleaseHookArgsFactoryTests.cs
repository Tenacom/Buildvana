// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Services.Hooks;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Utilities;
using NuGet.Versioning;

internal sealed class PostReleaseHookArgsFactoryTests
{
    // The args are the same wherever bv was invoked from: the factory resolves every path against the home
    // directory, never against the process's current directory (which bv anchors to home anyway).
    [Test]
    [NotInParallel]
    public async Task Create_ResolvesRelativeArtifactsPathAgainstHomeDirectory()
    {
        using var home = new TempHome();
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        var temporaryDirectory = Directory.CreateTempSubdirectory("bv-test-cwd-");
        try
        {
            Directory.SetCurrentDirectory(temporaryDirectory.FullName);

            var args = await CreateAsync(home.Provider, artifactsPath: Path.Combine("artifacts", "Release")).ConfigureAwait(false);

            await Assert.That(args.RuntimeInfo.ArtifactsDirectory)
                .IsEqualTo(Path.Combine(home.RootPath, "artifacts", "Release"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
            temporaryDirectory.Delete();
        }
    }

    [Test]
    public async Task Create_PreservesAbsoluteArtifactsPath()
    {
        using var home = new TempHome();
        var absolutePath = Path.Combine(home.RootPath, "artifacts", "Release");

        var args = await CreateAsync(home.Provider, artifactsPath: absolutePath).ConfigureAwait(false);

        await Assert.That(args.RuntimeInfo.ArtifactsDirectory).IsEqualTo(absolutePath);
    }

    [Test]
    public async Task Create_CopiesHomeDirectoryAndAnchorsScratchDirectoryToIt()
    {
        using var home = new TempHome();

        var args = await CreateAsync(home.Provider).ConfigureAwait(false);

        await Assert.That(args.RuntimeInfo.HomeDirectory).IsEqualTo(home.RootPath);
        await Assert.That(args.RuntimeInfo.ScratchDirectory)
            .IsEqualTo(Path.Combine(home.RootPath, WellKnownPaths.ScratchDirectory));
    }

    // Home directories carry a trailing separator by contract; the hook-facing path does not, so that a hook
    // can compare it with the working directory it runs in.
    [Test]
    public async Task Create_ReportsHomeDirectoryWithoutTrailingSeparator()
    {
        using var home = new TempHome();

        var args = await CreateAsync(new FixedHomeDirectoryProvider(home.RootPath + Path.DirectorySeparatorChar)).ConfigureAwait(false);

        await Assert.That(args.RuntimeInfo.HomeDirectory).IsEqualTo(home.RootPath);
    }

    // A hook rewriting the configuration file must act on the file bv read, so the args name it outright
    // rather than leaving the hook to search for it.
    [Test]
    [Arguments("buildvana.json")]
    [Arguments("buildvana.jsonc")]
    public async Task Create_ReportsTheConfigurationFileTheRunRead(string fileName)
    {
        using var home = new TempHome();
        home.WriteFile(fileName, "{}\n");

        var args = await CreateAsync(home.Provider).ConfigureAwait(false);

        await Assert.That(args.RuntimeInfo.ConfigFile).IsEqualTo(Path.Combine(home.RootPath, fileName));
    }

    [Test]
    public async Task Create_LeavesConfigFileNull_WhenRepositoryHasNone()
    {
        using var home = new TempHome();

        var args = await CreateAsync(home.Provider).ConfigureAwait(false);

        await Assert.That(args.RuntimeInfo.ConfigFile).IsNull();
    }

    // The factory embeds the resolved configuration as-is — a snapshot of the very settings the run
    // composed, not a re-read of the file.
    [Test]
    public async Task Create_EmbedsTheResolvedConfiguration()
    {
        using var home = new TempHome();
        var configuration = new BuildvanaConfig { Release = new() { Configuration = "Custom" } };

        var args = await CreateAsync(home.Provider, configuration: configuration).ConfigureAwait(false);

        await Assert.That(args.RuntimeInfo.Configuration).IsSameReferenceAs(configuration);
    }

    [Test]
    public async Task Create_SetsOwnVersionAsRuntimeVersion()
    {
        using var home = new TempHome();

        var args = await CreateAsync(home.Provider).ConfigureAwait(false);

        await Assert.That(args.RuntimeInfo.Version).IsEqualTo(OwnVersion.Value.ToNormalizedString());
    }

    [Test]
    [NotInParallel]
    public async Task Create_ReadsDelegatingVersionFromEnvironment()
    {
        using var home = new TempHome();
        var previousValue = Environment.GetEnvironmentVariable(DelegationService.DelegatedEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(DelegationService.DelegatedEnvVar, "9.9.9");
            var args = await CreateAsync(home.Provider).ConfigureAwait(false);
            await Assert.That(args.RuntimeInfo.DelegatingVersion).IsEqualTo("9.9.9");

            Environment.SetEnvironmentVariable(DelegationService.DelegatedEnvVar, null);
            args = await CreateAsync(home.Provider).ConfigureAwait(false);
            await Assert.That(args.RuntimeInfo.DelegatingVersion).IsNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(DelegationService.DelegatedEnvVar, previousValue);
        }
    }

    [Test]
    public async Task Create_MapsVersionFields()
    {
        using var home = new TempHome();

        var args = await CreateAsync(
            home.Provider,
            simpleVersion: "2.3.4",
            semVer: "2.3.4-rc.1",
            latest: SemanticVersion.Parse("2.3.3"),
            isPrerelease: true,
            isPublicRelease: false).ConfigureAwait(false);

        await Assert.That(args.Release.Version).IsEqualTo("2.3.4");
        await Assert.That(args.Release.SemVer).IsEqualTo("2.3.4-rc.1");
        await Assert.That(args.Release.PreviousVersion).IsEqualTo("2.3.3");
        await Assert.That(args.Release.IsPrerelease).IsTrue();
        await Assert.That(args.Release.IsPublicRelease).IsFalse();
    }

    [Test]
    public async Task Create_LeavesPreviousVersionNull_ForFirstRelease()
    {
        using var home = new TempHome();

        var args = await CreateAsync(home.Provider, latest: null).ConfigureAwait(false);

        await Assert.That(args.Release.PreviousVersion).IsNull();
    }

    [Test]
    public async Task Create_PassesProducedPackagesAndDogfoodingThrough()
    {
        using var home = new TempHome();
        var packages = new Dictionary<string, string> { ["Buildvana.Sdk"] = "2.3.4" };

        var args = await CreateAsync(home.Provider, producedPackages: packages, dogfooding: true).ConfigureAwait(false);

        await Assert.That(args.ProducedPackages.Count).IsEqualTo(1);
        await Assert.That(args.ProducedPackages["Buildvana.Sdk"]).IsEqualTo("2.3.4");
        await Assert.That(args.Dogfooding).IsTrue();
    }

    // The paths of the model name the files the release built, so the projects are evaluated in the
    // configuration the release builds in.
    [Test]
    public async Task Create_EvaluatesTheProjectsInTheReleaseConfiguration()
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", """<Solution><Project Path="src/A/A.csproj" /></Solution>""");
        var processRunner = new FakeProcessRunner();
        var configuration = new BuildvanaConfig { Release = new() { Configuration = "Custom" } };

        var args = await CreateAsync(home.Provider, configuration: configuration, processRunner: processRunner).ConfigureAwait(false);

        await Assert.That(args.Solution.Configuration).IsEqualTo("Custom");
        await Assert.That(args.Solution.Projects.Single().ProjectFile).IsEqualTo(Path.Combine(home.RootPath, "src", "A", "A.csproj"));
        await Assert.That(processRunner.Runs.Single().Args).Contains("-property:Configuration=Custom");
    }

    private static Task<PostReleaseHookArgs> CreateAsync(
        IHomeDirectoryProvider home,
        string? artifactsPath = null,
        string simpleVersion = "1.2.3",
        string semVer = "1.2.3-preview",
        SemanticVersion? latest = null,
        bool isPrerelease = true,
        bool isPublicRelease = false,
        IReadOnlyDictionary<string, string>? producedPackages = null,
        bool dogfooding = false,
        BuildvanaConfig? configuration = null,
        IProcessRunner? processRunner = null)
    {
        var solutions = new SolutionInfoReader(
            new HomeDirectorySolutionContextFactory(home),
            new SolutionTargetRunner(home, processRunner ?? new FakeProcessRunner(), NullReporter.Instance),
            NullReporter.Instance);

        var factory = new PostReleaseHookArgsFactory(home, new BuildvanaJsonConfigProvider(home), configuration ?? new(), solutions);
        return factory.CreateAsync(
            artifactsPath ?? Path.Combine("artifacts", "Release"),
            simpleVersion,
            semVer,
            latest,
            isPrerelease,
            isPublicRelease,
            producedPackages ?? new Dictionary<string, string>(),
            dogfooding);
    }
}
