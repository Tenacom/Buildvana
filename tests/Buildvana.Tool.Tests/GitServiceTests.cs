// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Testing;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.Git;

internal sealed class GitServiceTests
{
    [Test]
    public async Task GetDirtyFiles_OnCleanRepository_ReturnsEmpty()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);

        await Assert.That(git.GetDirtyFiles().Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetDirtyFiles_DetectsModifiedFile_AsAbsolutePath()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);
        repo.WriteFile("a.txt", "changed");

        var dirty = git.GetDirtyFiles();

        await Assert.That(dirty.Count).IsEqualTo(1);
        await Assert.That(dirty[0]).IsEqualTo(Path.Combine(repo.RootPath, "a.txt"));
    }

    [Test]
    public async Task GetDirtyFiles_DetectsUntrackedFile_InNewDirectory()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);
        var directory = Path.Combine(repo.RootPath, "new", "sub");
        _ = Directory.CreateDirectory(directory);
        var newFilePath = Path.Combine(directory, "b.txt");
        await File.WriteAllTextAsync(newFilePath, "content").ConfigureAwait(false);

        var dirty = git.GetDirtyFiles();

        await Assert.That(dirty.Count).IsEqualTo(1);
        await Assert.That(dirty[0]).IsEqualTo(newFilePath);
    }

    [Test]
    public async Task GetDirtyFiles_IgnoresScratchDirectory_EvenWhenNotGitignored()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);
        var directory = Path.Combine(repo.RootPath, CommonPaths.Scratch);
        _ = Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "hook-args.json"), "{}").ConfigureAwait(false);

        await Assert.That(git.GetDirtyFiles().Count).IsEqualTo(0);
    }

    [Test]
    public async Task TrackChangesAsync_ReturnsOperationResult_AndNoChangesOnUntouchedTree()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);

        var (result, changedFiles) = await git.TrackChangesAsync(() => Task.FromResult(42)).ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(changedFiles.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TrackChangesAsync_ReportsFilesModifiedByOperation_AsAbsolutePaths()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);
        var rootPath = repo.RootPath;

        var (_, changedFiles) = await git.TrackChangesAsync(async () => {
            await File.WriteAllTextAsync(Path.Combine(rootPath, "b.txt"), "new file").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(rootPath, "c.txt"), "another new file").ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

        var sorted = changedFiles.Order(StringComparer.Ordinal).ToArray();
        await Assert.That(sorted.Length).IsEqualTo(2);
        await Assert.That(sorted[0]).IsEqualTo(Path.Combine(repo.RootPath, "b.txt"));
        await Assert.That(sorted[1]).IsEqualTo(Path.Combine(repo.RootPath, "c.txt"));
    }

    [Test]
    public async Task TrackChangesAsync_ExcludesFilesAlreadyDirtyBeforeOperation()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);
        var rootPath = repo.RootPath;
        repo.WriteFile("a.txt", "dirty before the operation");

        var (_, changedFiles) = await git.TrackChangesAsync(async () => {
            await File.WriteAllTextAsync(Path.Combine(rootPath, "a.txt"), "dirty again during the operation").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(rootPath, "b.txt"), "new file").ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

        await Assert.That(changedFiles.Count).IsEqualTo(1);
        await Assert.That(changedFiles[0]).IsEqualTo(Path.Combine(repo.RootPath, "b.txt"));
    }

    [Test]
    public async Task TrackChangesAsync_WhenOperationThrows_Propagates()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the enclosing scope disposes git
        Task<(bool Result, IReadOnlyList<string> ChangedFiles)> Act()
            => git.TrackChangesAsync<bool>(() => throw new BuildFailedException("Operation failed."));

        _ = await Assert.That(Act).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Remotes_ListsEveryRemote_WithItsUrl()
    {
        using var repo = CreateRepoWithCommit();
        repo.AddRemote("upstream", new Uri("https://example.com/upstream.git"));
        using var git = CreateGitService(repo);

        await Assert.That(git.Remotes).IsEquivalentTo([
            new GitRemoteInfo("origin", "https://example.com/repo.git"),
            new GitRemoteInfo("upstream", "https://example.com/upstream.git"),
        ]);
    }

    // A clone with no remote is a repository all the same: commands that only read it work there.
    [Test]
    public async Task Constructor_WithoutRemotes_Succeeds()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("a.txt", "content");
        repo.CommitAll();
        using var git = CreateGitService(repo);

        await Assert.That(git.Remotes).IsEmpty();
    }

    [Test]
    public async Task CommitterIdentity_WhenSet_CommitsWithIt_AndWritesNothingToConfiguration()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);

        git.CommitterIdentity = new("Release Bot", "bot@example.invalid");
        git.Commit("Release commit", allowEmpty: true);

        var commit = repo.GetCommits(1)[0];
        await Assert.That(commit.CommitterName).IsEqualTo("Release Bot");
        await Assert.That(commit.CommitterEmail).IsEqualTo("bot@example.invalid");
        await Assert.That(repo.GetLocalConfigValue("user.name")).IsNull();
        await Assert.That(repo.GetLocalConfigValue("user.email")).IsNull();
    }

    [Test]
    public async Task CommitterIdentity_WhenNotSet_ReadsGitConfiguration()
    {
        using var repo = CreateRepoWithCommit();
        repo.SetCommitterIdentity("Local Committer", "local@example.invalid");
        using var git = CreateGitService(repo);

        await Assert.That(git.CommitterIdentity).IsEqualTo(new GitIdentity("Local Committer", "local@example.invalid"));
    }

    [Test]
    public async Task CommitterIdentity_WhenNeitherSetNorConfigured_IsNull()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);

        await Assert.That(git.CommitterIdentity).IsNull();
    }

    [Test]
    public async Task PushBranch_PushesCurrentBranch_ToHomonymOnRemote()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);

        git.PushBranch("origin");

        await Assert.That(repo.GetRemoteTipSha()).IsEqualTo(repo.HeadSha);
    }

    [Test]
    public async Task PushBranch_WhenNotFastForward_Throws()
    {
        using var repo = CreateRepoWithDivergedRemote(out var git);
        using (git)
        {
            var remoteTip = repo.GetRemoteTipSha();

            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
            _ = await Assert.That(() => git.PushBranch("origin")).ThrowsException();
            await Assert.That(repo.GetRemoteTipSha()).IsEqualTo(remoteTip);
        }
    }

    [Test]
    public async Task PushBranch_WithForce_ReplacesDivergedRemoteBranch()
    {
        using var repo = CreateRepoWithDivergedRemote(out var git);
        using (git)
        {
            git.PushBranch("origin", force: true);

            await Assert.That(repo.GetRemoteTipSha()).IsEqualTo(repo.HeadSha);
        }
    }

    [Test]
    public async Task PushBranch_WhenRemoteRefusesUpdate_Throws_AndLeavesRemoteBranchAlone()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");
        var remoteTip = repo.GetRemoteTipSha();
        git.CommitterIdentity = new("Release Bot", "bot@example.invalid");
        git.Commit("Refused commit", allowEmpty: true);
        var branchRef = $"refs/heads/{repo.CurrentBranchName}";
        repo.LockRemoteReference(branchRef);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        var exception = await Assert.That(() => git.PushBranch("origin")).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains(branchRef);
        await Assert.That(repo.GetRemoteTipSha()).IsEqualTo(remoteTip);
    }

    [Test]
    public async Task PushBranch_WhenHeadIsDetached_Throws()
    {
        using var repo = CreateRepoWithBareRemote();
        repo.CheckoutDetached();
        using var git = CreateGitService(repo);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        _ = await Assert.That(() => git.PushBranch("origin")).Throws<BuildFailedException>();
    }

    [Test]
    public async Task PushBranch_ToMissingRemote_Throws()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        var exception = await Assert.That(() => git.PushBranch("upstream")).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("'upstream'");
    }

    [Test]
    public async Task PushTag_PutsTagOnRemote_AtTheCommitItWasCreatedAt()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        var taggedSha = repo.HeadSha;
        git.CommitterIdentity = new("Release Bot", "bot@example.invalid");
        git.Commit("Post-release commit", allowEmpty: true);
        git.PushBranch("origin");

        git.CreateTag("1.0.0", taggedSha);
        git.PushTag("origin", "1.0.0");

        await Assert.That(repo.TagNames).IsEquivalentTo(["1.0.0"]);
        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsEqualTo(taggedSha);
    }

    [Test]
    public async Task PushTag_WhenRemoteTagPointsToDescendant_Throws()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        var firstSha = repo.HeadSha;
        git.CommitterIdentity = new("Release Bot", "bot@example.invalid");
        git.Commit("Second commit", allowEmpty: true);
        git.PushBranch("origin");
        repo.CreateRemoteTag("1.0.0");
        git.CreateTag("1.0.0", firstSha);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        _ = await Assert.That(() => git.PushTag("origin", "1.0.0")).Throws<BuildFailedException>();
        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsEqualTo(repo.HeadSha);
    }

    [Test]
    public async Task PushTag_WhenRemoteTagPointsToAncestor_Throws_AndLeavesRemoteTagAlone()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");
        var firstSha = repo.HeadSha;
        repo.CreateRemoteTag("1.0.0");
        git.CommitterIdentity = new("Release Bot", "bot@example.invalid");
        git.Commit("Second commit", allowEmpty: true);
        git.PushBranch("origin");
        git.CreateTag("1.0.0", repo.HeadSha);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        var exception = await Assert.That(() => git.PushTag("origin", "1.0.0")).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains(firstSha);
        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsEqualTo(firstSha);
    }

    [Test]
    public async Task PushTag_WhenRemoteTagPointsToSameCommit_Succeeds()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");
        repo.CreateRemoteTag("1.0.0");
        git.CreateTag("1.0.0", repo.HeadSha);

        git.PushTag("origin", "1.0.0");

        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsEqualTo(repo.HeadSha);
    }

    [Test]
    public async Task PushTag_WhenLocalTagDoesNotExist_Throws()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        _ = await Assert.That(() => git.PushTag("origin", "1.0.0")).Throws<BuildFailedException>();
        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsNull();
    }

    [Test]
    public async Task PushTag_WhenRemoteRefusesUpdate_Throws()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");
        git.CreateTag("1.0.0", repo.HeadSha);
        repo.LockRemoteReference("refs/tags/1.0.0");

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        var exception = await Assert.That(() => git.PushTag("origin", "1.0.0")).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("refs/tags/1.0.0");
        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsNull();
    }

    [Test]
    public async Task DeleteRemoteTag_RemovesTagFromRemote_AndKeepsLocalTag()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");
        git.CreateTag("1.0.0", repo.HeadSha);
        git.PushTag("origin", "1.0.0");

        git.DeleteRemoteTag("origin", "1.0.0");

        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsNull();
        await Assert.That(repo.TagNames).IsEquivalentTo(["1.0.0"]);
    }

    [Test]
    public async Task DeleteRemoteTag_WhenRemoteRefusesUpdate_Throws_AndKeepsRemoteTag()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");
        git.CreateTag("1.0.0", repo.HeadSha);
        git.PushTag("origin", "1.0.0");
        repo.LockRemoteReference("refs/tags/1.0.0");

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before git is disposed
        var exception = await Assert.That(() => git.DeleteRemoteTag("origin", "1.0.0")).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("refs/tags/1.0.0");
        await Assert.That(repo.GetRemoteTagSha("1.0.0")).IsEqualTo(repo.HeadSha);
    }

    [Test]
    public async Task DeleteTag_RemovesLocalTag()
    {
        using var repo = CreateRepoWithCommit();
        using var git = CreateGitService(repo);
        git.CreateTag("1.0.0", repo.HeadSha);

        git.DeleteTag("1.0.0");

        await Assert.That(repo.TagNames.Count).IsEqualTo(0);
        await Assert.That(git.TagExists("1.0.0")).IsFalse();
    }

    [Test]
    public async Task FetchTags_BringsTagsOfRemote_IntoLocalRepository()
    {
        using var repo = CreateRepoWithBareRemote();
        using var git = CreateGitService(repo);
        git.PushBranch("origin");
        repo.CreateRemoteTag("1.0.0");

        git.FetchTags("origin");

        await Assert.That(git.TagExists("1.0.0")).IsTrue();
        await Assert.That(repo.TagNames).IsEquivalentTo(["1.0.0"]);
    }

    private static TempGitRepo CreateRepoWithCommit()
    {
        var repo = new TempGitRepo();
        repo.AddRemote("origin", new Uri("https://example.com/repo.git"));
        repo.WriteFile("a.txt", "content");
        repo.CommitAll();
        return repo;
    }

    private static TempGitRepo CreateRepoWithBareRemote()
    {
        var repo = new TempGitRepo();
        repo.WriteFile("a.txt", "content");
        repo.CommitAll();
        _ = repo.AddBareRemote();
        return repo;
    }

    // The remote ends up with a commit that the local branch does not have, and the local branch with one that the
    // remote does not have.
    private static TempGitRepo CreateRepoWithDivergedRemote(out GitService git)
    {
        var repo = CreateRepoWithBareRemote();
        git = CreateGitService(repo);
        git.CommitterIdentity = new("Release Bot", "bot@example.invalid");
        git.Commit("Pushed commit", allowEmpty: true);
        git.PushBranch("origin");
        git.UndoLastCommit();
        repo.WriteFile("a.txt", "diverged");
        repo.CommitAll("Diverged commit");
        return repo;
    }

    private static GitService CreateGitService(TempGitRepo repo)
        => new(NullReporter.Instance, new FixedHomeDirectoryProvider(repo.RootPath));
}
