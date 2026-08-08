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

        var (_, changedFiles) = await git.TrackChangesAsync(async () =>
        {
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

        var (_, changedFiles) = await git.TrackChangesAsync(async () =>
        {
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

    private static TempGitRepo CreateRepoWithCommit()
    {
        var repo = new TempGitRepo();
        repo.AddRemote("origin", new Uri("https://example.com/repo.git"));
        repo.WriteFile("a.txt", "content");
        repo.CommitAll();
        return repo;
    }

    private static GitService CreateGitService(TempGitRepo repo)
        => new(NullReporter.Instance, new FixedHomeDirectoryProvider(repo.RootPath));
}
