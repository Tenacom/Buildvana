// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Services.Hooks;
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

            var args = Create(home.Provider, artifactsPath: Path.Combine("artifacts", "Release"));

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

        var args = Create(home.Provider, artifactsPath: absolutePath);

        await Assert.That(args.RuntimeInfo.ArtifactsDirectory).IsEqualTo(absolutePath);
    }

    [Test]
    public async Task Create_CopiesHomeDirectoryAndAnchorsScratchDirectoryToIt()
    {
        using var home = new TempHome();

        var args = Create(home.Provider);

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

        var args = Create(new FixedHomeDirectoryProvider(home.RootPath + Path.DirectorySeparatorChar));

        await Assert.That(args.RuntimeInfo.HomeDirectory).IsEqualTo(home.RootPath);
    }

    [Test]
    public async Task Create_SetsOwnVersionAsRuntimeVersion()
    {
        using var home = new TempHome();

        var args = Create(home.Provider);

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
            var args = Create(home.Provider);
            await Assert.That(args.RuntimeInfo.DelegatingVersion).IsEqualTo("9.9.9");

            Environment.SetEnvironmentVariable(DelegationService.DelegatedEnvVar, null);
            args = Create(home.Provider);
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

        var args = Create(
            home.Provider,
            simpleVersion: "2.3.4",
            semVer: "2.3.4-rc.1",
            latest: SemanticVersion.Parse("2.3.3"),
            isPrerelease: true,
            isPublicRelease: false);

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

        var args = Create(home.Provider, latest: null);

        await Assert.That(args.Release.PreviousVersion).IsNull();
    }

    [Test]
    public async Task Create_PassesProducedPackagesAndDogfoodedThrough()
    {
        using var home = new TempHome();
        var packages = new Dictionary<string, string> { ["Buildvana.Sdk"] = "2.3.4" };

        var args = Create(home.Provider, producedPackages: packages, dogfooded: true);

        await Assert.That(args.ProducedPackages.Count).IsEqualTo(1);
        await Assert.That(args.ProducedPackages["Buildvana.Sdk"]).IsEqualTo("2.3.4");
        await Assert.That(args.Dogfooded).IsTrue();
    }

    private static PostReleaseHookArgs Create(
        IHomeDirectoryProvider home,
        string? artifactsPath = null,
        string simpleVersion = "1.2.3",
        string semVer = "1.2.3-preview",
        SemanticVersion? latest = null,
        bool isPrerelease = true,
        bool isPublicRelease = false,
        IReadOnlyDictionary<string, string>? producedPackages = null,
        bool dogfooded = false)
        => new PostReleaseHookArgsFactory(home).Create(
            artifactsPath ?? Path.Combine("artifacts", "Release"),
            simpleVersion,
            semVer,
            latest,
            isPrerelease,
            isPublicRelease,
            producedPackages ?? new Dictionary<string, string>(),
            dogfooded);
}
