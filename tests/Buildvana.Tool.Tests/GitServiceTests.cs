// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

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
