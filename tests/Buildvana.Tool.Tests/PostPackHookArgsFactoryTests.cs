// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Hooks;
using Buildvana.Tool.Services.Solution;
using NuGet.Versioning;

// PostReleaseHookArgsFactoryTests covers the RuntimeInfo section, which HookArgsFactory<TArgs> assembles for
// every hook. These tests cover what the factory of the release/post-pack hook adds.
internal sealed class PostPackHookArgsFactoryTests
{
    [Test]
    public async Task Create_MapsVersionFields()
    {
        using var home = new TempHome();

        var args = await CreateAsync(
            home,
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

        var args = await CreateAsync(home, latest: null).ConfigureAwait(false);

        await Assert.That(args.Release.PreviousVersion).IsNull();
    }

    [Test]
    public async Task Create_PassesProducedPackagesThrough()
    {
        using var home = new TempHome();
        var packages = new Dictionary<string, string> { ["Buildvana.Sdk"] = "2.3.4" };

        var args = await CreateAsync(home, producedPackages: packages).ConfigureAwait(false);

        await Assert.That(args.ProducedPackages.Count).IsEqualTo(1);
        await Assert.That(args.ProducedPackages["Buildvana.Sdk"]).IsEqualTo("2.3.4");
    }

    // The hook reads the assemblies the release built, so the projects are evaluated in the configuration
    // the release builds in.
    [Test]
    public async Task Create_EvaluatesTheProjectsInTheReleaseConfiguration()
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", """<Solution><Project Path="src/A/A.csproj" /></Solution>""");
        var processRunner = new FakeProcessRunner();
        var configuration = new BuildvanaConfig { Release = new() { Configuration = "Custom" } };

        var args = await CreateAsync(home, configuration: configuration, processRunner: processRunner).ConfigureAwait(false);

        await Assert.That(args.Solution.Configuration).IsEqualTo("Custom");
        await Assert.That(args.Solution.Projects.Single().ProjectFile).IsEqualTo(Path.Combine(home.RootPath, "src", "A", "A.csproj"));
        await Assert.That(processRunner.Runs.Single().Args).Contains("-property:Configuration=Custom");
        await Assert.That(args.RuntimeInfo.ArtifactsDirectory).IsEqualTo(Path.Combine(home.RootPath, "artifacts", "Release"));
    }

    private static Task<PostPackHookArgs> CreateAsync(
        TempHome home,
        string simpleVersion = "1.2.3",
        string semVer = "1.2.3-preview",
        SemanticVersion? latest = null,
        bool isPrerelease = true,
        bool isPublicRelease = false,
        IReadOnlyDictionary<string, string>? producedPackages = null,
        BuildvanaConfig? configuration = null,
        IProcessRunner? processRunner = null)
    {
        var solutions = new SolutionInfoReader(
            new HomeDirectorySolutionContextFactory(home.Provider),
            new SolutionTargetRunner(home.Provider, processRunner ?? new FakeProcessRunner(), NullReporter.Instance),
            NullReporter.Instance);

        var factory = new PostPackHookArgsFactory(
            home.Provider,
            new BuildvanaJsonConfigProvider(home.Provider),
            configuration ?? new(),
            solutions);
        return factory.CreateAsync(
            Path.Combine("artifacts", "Release"),
            simpleVersion,
            semVer,
            latest,
            isPrerelease,
            isPublicRelease,
            producedPackages ?? new Dictionary<string, string>());
    }
}
