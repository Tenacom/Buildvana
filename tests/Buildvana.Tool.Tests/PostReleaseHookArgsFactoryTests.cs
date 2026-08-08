// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Services.Hooks;
using Buildvana.Tool.Utilities;
using NuGet.Versioning;

internal sealed class PostReleaseHookArgsFactoryTests
{
    private static readonly string TestHome = Path.Combine(Path.GetTempPath(), "bv-test-home");

    [Test]
    [NotInParallel]
    public async Task Create_AnchorsRelativeArtifactsPathToCurrentDirectory()
    {
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        var temporaryDirectory = Directory.CreateTempSubdirectory("bv-test-cwd-");
        try
        {
            Directory.SetCurrentDirectory(temporaryDirectory.FullName);
            var currentDirectory = Directory.GetCurrentDirectory();

            var args = Create(artifactsPath: Path.Combine("artifacts", "Release"));

            await Assert.That(args.RuntimeInfo.ArtifactsDirectory)
                .IsEqualTo(Path.Combine(currentDirectory, "artifacts", "Release"));
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
        var absolutePath = Path.Combine(TestHome, "artifacts", "Release");

        var args = Create(artifactsPath: absolutePath);

        await Assert.That(args.RuntimeInfo.ArtifactsDirectory).IsEqualTo(absolutePath);
    }

    [Test]
    public async Task Create_CopiesHomeDirectoryAndAnchorsScratchDirectoryToIt()
    {
        var args = Create();

        await Assert.That(args.RuntimeInfo.HomeDirectory).IsEqualTo(TestHome);
        await Assert.That(args.RuntimeInfo.ScratchDirectory)
            .IsEqualTo(Path.Combine(TestHome, WellKnownPaths.ScratchDirectory));
    }

    [Test]
    public async Task Create_SetsOwnVersionAsRuntimeVersion()
    {
        var args = Create();

        await Assert.That(args.RuntimeInfo.Version).IsEqualTo(OwnVersion.Value.ToNormalizedString());
    }

    [Test]
    [NotInParallel]
    public async Task Create_ReadsDelegatingVersionFromEnvironment()
    {
        var previousValue = Environment.GetEnvironmentVariable(DelegationService.DelegatedEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(DelegationService.DelegatedEnvVar, "9.9.9");
            var args = Create();
            await Assert.That(args.RuntimeInfo.DelegatingVersion).IsEqualTo("9.9.9");

            Environment.SetEnvironmentVariable(DelegationService.DelegatedEnvVar, null);
            args = Create();
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
        var args = Create(
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
        var args = Create(latest: null);

        await Assert.That(args.Release.PreviousVersion).IsNull();
    }

    [Test]
    public async Task Create_PassesProducedPackagesAndDogfoodedThrough()
    {
        var packages = new Dictionary<string, string> { ["Buildvana.Sdk"] = "2.3.4" };

        var args = Create(producedPackages: packages, dogfooded: true);

        await Assert.That(args.ProducedPackages.Count).IsEqualTo(1);
        await Assert.That(args.ProducedPackages["Buildvana.Sdk"]).IsEqualTo("2.3.4");
        await Assert.That(args.Dogfooded).IsTrue();
    }

    private static PostReleaseHookArgs Create(
        string? homeDirectory = null,
        string? artifactsPath = null,
        string simpleVersion = "1.2.3",
        string semVer = "1.2.3-preview",
        SemanticVersion? latest = null,
        bool isPrerelease = true,
        bool isPublicRelease = false,
        IReadOnlyDictionary<string, string>? producedPackages = null,
        bool dogfooded = false)
        => PostReleaseHookArgsFactory.Create(
            homeDirectory ?? TestHome,
            artifactsPath ?? Path.Combine("artifacts", "Release"),
            simpleVersion,
            semVer,
            latest,
            isPrerelease,
            isPublicRelease,
            producedPackages ?? new Dictionary<string, string>(),
            dogfooded);
}
