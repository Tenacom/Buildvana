// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Testing;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;

internal sealed class VersionCalculatorTests
{
    [Test]
    public async Task Calculate_MissingVersionFile_Throws()
    {
        using var repo = new TempGitRepo();
        repo.CommitAll();
        var exception = CatchCalculate(repo, new BuildvanaConfig());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("VERSION");
    }

    [Test]
    public async Task Calculate_MalformedVersionFile_Throws()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "banana\n");
        repo.CommitAll();
        var exception = CatchCalculate(repo, new BuildvanaConfig());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("banana");
    }

    [Test]
    public async Task Calculate_PrereleaseWithoutTag_Throws()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0-\n");
        repo.CommitAll();
        var exception = CatchCalculate(repo, new BuildvanaConfig());
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("versioning.prereleaseTag");
    }

    [Test]
    public async Task Calculate_InvalidPrereleaseTag_Throws()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0-\n");
        repo.CommitAll();
        var config = new BuildvanaConfig { Versioning = new VersioningConfig { PrereleaseTag = "not valid" } };
        var exception = CatchCalculate(repo, config);
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("not valid");
    }

    [Test]
    public async Task Calculate_StablePublicRelease_ComputesVersionStrings()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        repo.CheckoutNewBranch("rel");
        repo.CommitAll();
        var config = new BuildvanaConfig { Release = new ReleaseConfig { Branches = ["^rel$"] } };
        var version = CalculateVersion(repo, config);
        await Assert.That(version.Spec).IsEqualTo(new VersionSpec(2, 3, false));
        await Assert.That(version.Height).IsEqualTo(2);
        await Assert.That(version.IsPublicRelease).IsTrue();
        await Assert.That(version.IsPrerelease).IsFalse();
        await Assert.That(version.SimpleVersion).IsEqualTo("2.3.2");
        await Assert.That(version.SemVer).IsEqualTo("2.3.2");
        await Assert.That(version.InformationalVersion).IsEqualTo("2.3.2");
        await Assert.That(version.CommitId).IsEqualTo(repo.HeadSha);
    }

    [Test]
    public async Task Calculate_PrereleasePublicRelease_AppendsTagOnly()
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
        var version = CalculateVersion(repo, config);
        await Assert.That(version.IsPrerelease).IsTrue();
        await Assert.That(version.SemVer).IsEqualTo("2.3.1-preview");
        await Assert.That(version.InformationalVersion).IsEqualTo("2.3.1-preview");
    }

    [Test]
    public async Task Calculate_PrereleaseNonPublic_AppendsCommitIdToInformationalVersion()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3-x\n");
        repo.CommitAll();
        var config = new BuildvanaConfig { Versioning = new VersioningConfig { PrereleaseTag = "preview" } };
        var version = CalculateVersion(repo, config);
        await Assert.That(version.IsPublicRelease).IsFalse();
        await Assert.That(version.SemVer).IsEqualTo("2.3.1-preview");
        await Assert.That(version.InformationalVersion).IsEqualTo($"2.3.1-preview.g{repo.HeadSha[..10]}");
    }

    [Test]
    public async Task Calculate_StableNonPublic_AppendsCommitIdAsPrerelease()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var version = CalculateVersion(repo, new BuildvanaConfig());
        await Assert.That(version.SemVer).IsEqualTo("2.3.1");
        await Assert.That(version.InformationalVersion).IsEqualTo($"2.3.1-g{repo.HeadSha[..10]}");
    }

    [Test]
    [Arguments(AssemblyVersionPrecision.Major, "2.0.0.0")]
    [Arguments(AssemblyVersionPrecision.Minor, "2.3.0.0")]
    [Arguments(AssemblyVersionPrecision.Build, "2.3.1.0")]
    public async Task Calculate_AssemblyVersionHonorsPrecision(AssemblyVersionPrecision precision, string expected)
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var config = new BuildvanaConfig { Versioning = new VersioningConfig { AssemblyVersionPrecision = precision } };
        var version = CalculateVersion(repo, config);
        await Assert.That(version.AssemblyVersion).IsEqualTo(expected);
    }

    [Test]
    public async Task Calculate_DefaultPrecision_IsMajor()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var version = CalculateVersion(repo, new BuildvanaConfig());
        await Assert.That(version.AssemblyVersion).IsEqualTo("2.0.0.0");
    }

    [Test]
    public async Task Calculate_FileVersion_IsFullPrecisionRegardlessOfAssemblyVersionPrecision()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "2.3\n");
        repo.CommitAll();
        var version = CalculateVersion(repo, new BuildvanaConfig());
        await Assert.That(version.FileVersion).IsEqualTo("2.3.1.0");
    }

    private static VersionInfo CalculateVersion(TempGitRepo repo, BuildvanaConfig config)
        => new VersionCalculator(
            new FixedHomeDirectoryProvider(repo.RootPath),
            new VersioningSettings(config),
            new GitHeightCalculator(VersionFile.FileName)).Calculate();

    private static BuildFailedException? CatchCalculate(TempGitRepo repo, BuildvanaConfig config)
    {
        try
        {
            _ = CalculateVersion(repo, config);
            return null;
        }
        catch (BuildFailedException e)
        {
            return e;
        }
    }
}
