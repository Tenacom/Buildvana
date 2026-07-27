// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Versioning;

internal sealed class GitHeightCalculatorTests
{
    [Test]
    public async Task Calculate_WithoutRepository_ThrowsBuildFailedException()
    {
        var directory = Directory.CreateTempSubdirectory("bv-test-");
        try
        {
            var calculator = new GitHeightCalculator("VERSION");
            BuildFailedException? exception = null;
            try
            {
                _ = calculator.Calculate(directory.FullName, new VersionSpec(1, 0, false));
            }
            catch (BuildFailedException e)
            {
                exception = e;
            }

            await Assert.That(exception).IsNotNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Calculate_UnbornHead_ReturnsZeroHeightAndNoCommit()
    {
        using var repo = new TempGitRepo();
        var result = Calculate(repo, 1, 0);
        await Assert.That(result).IsEqualTo(new GitHeightResult(0, string.Empty, null));
    }

    [Test]
    public async Task Calculate_CommitIntroducingVersionFile_HasHeightOne()
    {
        using var repo = new TempGitRepo();
        repo.CommitAll();
        repo.CommitAll();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_CommitsOnLine_IncrementHeight()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(2);
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(3);
    }

    [Test]
    public async Task Calculate_MajorMinorBump_ResetsHeight()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        repo.CommitAll();
        repo.WriteFile("VERSION", "1.1\n");
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 1).Height).IsEqualTo(1);
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 1).Height).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_PrereleaseOnlyChanges_DoNotResetHeight()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0-alpha\n");
        repo.CommitAll();
        repo.CommitAll();
        repo.WriteFile("VERSION", "1.0-beta\n");
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(3);
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(4);
    }

    [Test]
    public async Task Calculate_VersionFileNotCommitted_ReturnsZeroHeight()
    {
        using var repo = new TempGitRepo();
        repo.CommitAll();
        repo.WriteFile("VERSION", "1.0\n");
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(0);
    }

    [Test]
    public async Task Calculate_UncommittedMajorMinorBump_ReturnsZeroHeight()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 1).Height).IsEqualTo(0);
    }

    [Test]
    public async Task Calculate_MergeCommit_UsesLongestPath()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        var mainline = repo.CurrentBranchName;
        repo.CheckoutNewBranch("side");
        repo.CommitAll();
        repo.CommitAll();
        repo.CommitAll();
        repo.Checkout(mainline);
        repo.CommitAll();
        repo.Merge("side");
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(5);
    }

    [Test]
    public async Task Calculate_MalformedCommittedVersionFile_StopsWalk()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "banana\n");
        repo.CommitAll();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(1);
    }

    [Test]
    public async Task Calculate_CommittedVersionFileWithBom_DoesNotStopWalk()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();

        // File.WriteAllText encodes the leading U+FEFF as the UTF-8 BOM bytes on disk.
        repo.WriteFile("VERSION", "\uFEFF1.0\n");
        repo.CommitAll();
        await Assert.That(Calculate(repo, 1, 0).Height).IsEqualTo(2);
    }

    [Test]
    public async Task Calculate_OnBranch_ReportsBranchNameAndCommit()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        var result = Calculate(repo, 1, 0);
        await Assert.That(result.BranchName).IsEqualTo(repo.CurrentBranchName);
        await Assert.That(result.CommitId).IsEqualTo(repo.HeadSha);
    }

    [Test]
    public async Task Calculate_DetachedHead_ReportsEmptyBranchName()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.0\n");
        repo.CommitAll();
        repo.CheckoutDetached();
        var result = Calculate(repo, 1, 0);
        await Assert.That(result.BranchName).IsEqualTo(string.Empty);
        await Assert.That(result.Height).IsEqualTo(1);
    }

    private static GitHeightResult Calculate(TempGitRepo repo, int major, int minor)
        => new GitHeightCalculator("VERSION").Calculate(repo.RootPath, new VersionSpec(major, minor, false));
}
