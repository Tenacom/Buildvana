// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Testing;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;

internal sealed class VersioningServiceTests
{
    [Test]
    public async Task Constructor_MissingVersionFile_Throws()
    {
        using var repo = new TempGitRepo();
        repo.CommitAll();
        var exception = CatchCreate(repo, new BuildvanaConfig());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("VERSION");
    }

    [Test]
    public async Task Constructor_MalformedVersionFile_Throws()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "banana\n");
        repo.CommitAll();
        var exception = CatchCreate(repo, new BuildvanaConfig());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("banana");
    }

    [Test]
    public async Task Constructor_PrereleaseWithoutTag_Throws()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0-\n");
        repo.CommitAll();
        var exception = CatchCreate(repo, new BuildvanaConfig());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("versioning.prereleaseTag");
    }

    [Test]
    public async Task Constructor_InvalidPrereleaseTag_Throws()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0-\n");
        repo.CommitAll();
        var config = new BuildvanaConfig { Versioning = new VersioningConfig { PrereleaseTag = "not valid" } };
        var exception = CatchCreate(repo, config);
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("not valid");
    }

    [Test]
    public async Task Constructor_StablePublicRelease_ComputesVersionStrings()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        repo.CheckoutNewBranch("rel");
        repo.CommitAll();
        var config = new BuildvanaConfig { Release = new ReleaseConfig { Branches = ["^rel$"] } };
        var service = CreateService(repo, config);
        await Assert.That(service.Spec).IsEqualTo(new VersionSpec(2, 3, false));
        await Assert.That(service.Height).IsEqualTo(2);
        await Assert.That(service.IsPublicRelease).IsTrue();
        await Assert.That(service.IsPrerelease).IsFalse();
        await Assert.That(service.SimpleVersion).IsEqualTo("2.3.2");
        await Assert.That(service.SemVer).IsEqualTo("2.3.2");
        await Assert.That(service.InformationalVersion).IsEqualTo("2.3.2");
        await Assert.That(service.CommitId).IsEqualTo(repo.HeadSha);
    }

    [Test]
    public async Task Constructor_PrereleasePublicRelease_AppendsTagOnly()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3-x\n");
        repo.CommitAll();
        repo.CheckoutNewBranch("rel");
        var config = new BuildvanaConfig
        {
            Release = new ReleaseConfig { Branches = ["^rel$"] },
            Versioning = new VersioningConfig { PrereleaseTag = "preview" },
        };
        var service = CreateService(repo, config);
        await Assert.That(service.IsPrerelease).IsTrue();
        await Assert.That(service.SemVer).IsEqualTo("2.3.1-preview");
        await Assert.That(service.InformationalVersion).IsEqualTo("2.3.1-preview");
    }

    [Test]
    public async Task Constructor_PrereleaseNonPublic_AppendsCommitIdToInformationalVersion()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3-x\n");
        repo.CommitAll();
        var config = new BuildvanaConfig { Versioning = new VersioningConfig { PrereleaseTag = "preview" } };
        var service = CreateService(repo, config);
        await Assert.That(service.IsPublicRelease).IsFalse();
        await Assert.That(service.SemVer).IsEqualTo("2.3.1-preview");
        await Assert.That(service.InformationalVersion).IsEqualTo($"2.3.1-preview.g{repo.HeadSha[..10]}");
    }

    [Test]
    public async Task Constructor_StableNonPublic_AppendsCommitIdAsPrerelease()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var service = CreateService(repo, new BuildvanaConfig());
        await Assert.That(service.SemVer).IsEqualTo("2.3.1");
        await Assert.That(service.InformationalVersion).IsEqualTo($"2.3.1-g{repo.HeadSha[..10]}");
    }

    [Test]
    [Arguments(AssemblyVersionPrecision.Major, "2.0.0.0")]
    [Arguments(AssemblyVersionPrecision.Minor, "2.3.0.0")]
    [Arguments(AssemblyVersionPrecision.Build, "2.3.1.0")]
    public async Task Constructor_AssemblyVersionHonorsPrecision(AssemblyVersionPrecision precision, string expected)
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var config = new BuildvanaConfig { Versioning = new VersioningConfig { AssemblyVersionPrecision = precision } };
        var service = CreateService(repo, config);
        await Assert.That(service.AssemblyVersion).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_DefaultPrecision_IsMajor()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var service = CreateService(repo, new BuildvanaConfig());
        await Assert.That(service.AssemblyVersion).IsEqualTo("2.0.0.0");
    }

    [Test]
    public async Task Constructor_FileVersion_IsFullPrecisionRegardlessOfAssemblyVersionPrecision()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var service = CreateService(repo, new BuildvanaConfig());
        await Assert.That(service.FileVersion).IsEqualTo("2.3.1.0");
    }

    private static VersioningService CreateService(TempGitRepo repo, BuildvanaConfig config)
        => new(
            NullReporter.Instance,
            new FixedHomeDirectoryProvider(repo.RootPath),
            new VersioningSettings(config),
            new GitHeightCalculator(VersionFile.FileName));

    private static BuildFailedException? CatchCreate(TempGitRepo repo, BuildvanaConfig config)
    {
        try
        {
            _ = CreateService(repo, config);
            return null;
        }
        catch (BuildFailedException e)
        {
            return e;
        }
    }
}
