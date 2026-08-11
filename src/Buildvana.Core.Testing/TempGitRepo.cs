// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;

namespace Buildvana.Core.Testing;

/// <summary>
/// A disposable real Git repository in a temporary directory, for tests exercising Git-dependent code.
/// </summary>
/// <remarks>
/// <para>Constructing the first instance in a process points libgit2's configuration search paths at an empty
/// directory, so that no repository created by this class — and no code reading one — can see the machine's
/// global, XDG, or system Git configuration. Without it, a test would observe a committer identity on a
/// developer laptop and none on a bare CI runner, and the two would exercise different code paths.</para>
/// <para>Every member that reads the repository's state opens a handle of its own, and none of them reads
/// through the handle this class keeps for the operations that change it. Code under test holds a handle of
/// its own too, so anything it commits, tags, or checks out has to be observable here no matter what either
/// handle happens to have cached.</para>
/// </remarks>
public sealed class TempGitRepo : IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Repository _repository;
    private readonly Dictionary<string, string> _remotePaths = new(StringComparer.Ordinal);

    // Isolates the configuration once per process. A type initializer runs before the first instance is
    // created, and blocks every other thread until it has completed, so no thread can be inside libgit2 —
    // which keeps the search paths in a global — while they are being changed. Doing this per instance
    // instead would mutate that global underneath repositories other threads are already using, which
    // libgit2 answers with a corrupted heap.
    static TempGitRepo()
    {
        // An empty directory of this process's own: libgit2 looks for configuration files in it and finds
        // none, which is the whole of the isolation. A fixed path under the temp directory would do the same
        // job right up to the moment anything else dropped a .gitconfig into it — the temp directory is
        // shared between users on Linux — and the isolation would then be quietly turned into its opposite.
        var path = Directory.CreateTempSubdirectory("bv-test-gitconfig-").FullName;
        ReadOnlySpan<ConfigurationLevel> levels = [ConfigurationLevel.Global, ConfigurationLevel.Xdg, ConfigurationLevel.System];
        foreach (var level in levels)
        {
            GlobalSettings.SetConfigSearchPaths(level, path);
        }

        // The directory has to outlive every repository, because the search paths cannot be restored while
        // the process may still open one: changing them again is the race this type initializer exists to
        // avoid. Process exit is therefore the first moment the directory can go, and the last one at which
        // anything of ours still runs to delete it.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An empty directory left behind in the temp path is not worth failing a test run over.
            }
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TempGitRepo"/> class, creating and initializing
    /// a Git repository in a newly-created temporary directory.
    /// </summary>
    public TempGitRepo()
    {
        RootPath = Directory.CreateTempSubdirectory("bv-test-repo-").FullName;
        _ = Repository.Init(RootPath);
        _repository = new Repository(RootPath);
    }

    /// <summary>
    /// Gets the full path of the repository's root directory.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Gets the friendly name of the current branch.
    /// </summary>
    public string CurrentBranchName
    {
        get
        {
            using var repository = new Repository(RootPath);
            return repository.Head.FriendlyName;
        }
    }

    /// <summary>
    /// Gets the SHA of the current <c>HEAD</c> commit.
    /// </summary>
    public string HeadSha
    {
        get
        {
            using var repository = new Repository(RootPath);
            return repository.Head.Tip.Sha;
        }
    }

    /// <summary>
    /// Gets the friendly names of the repository's tags.
    /// </summary>
    public IReadOnlyList<string> TagNames
    {
        get
        {
            using var repository = new Repository(RootPath);
            return [.. repository.Tags.Select(x => x.FriendlyName).Order(StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// Writes a file in the repository's root directory.
    /// </summary>
    /// <param name="name">The name of the file, relative to the root directory.</param>
    /// <param name="content">The content of the file.</param>
    /// <param name="encoding">The file encoding; UTF-8 without BOM if unspecified.</param>
    public void WriteFile(string name, string content, Encoding? encoding = null)
        => File.WriteAllText(Path.Combine(RootPath, name), content, encoding ?? Utf8NoBom);

    /// <summary>
    /// Adds a remote to the repository.
    /// </summary>
    /// <param name="name">The name of the remote.</param>
    /// <param name="url">The URL of the remote.</param>
    public void AddRemote(string name, Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        _ = _repository.Network.Remotes.Add(name, url.AbsoluteUri);
    }

    /// <summary>
    /// Creates a bare Git repository in a temporary directory of its own, adds it as a remote, and makes
    /// the current branch track its homonym on it, so that pushes from the current branch actually go
    /// somewhere and can be inspected afterwards (see <see cref="GetRemoteTipSha"/>).
    /// </summary>
    /// <param name="name">The name of the remote.</param>
    /// <returns>The full path of the bare repository.</returns>
    /// <remarks>
    /// <para>The repository must already have a commit, so that there is a branch to set tracking information on.</para>
    /// <para>The bare repository lives outside <see cref="RootPath"/>, so that it does not show up as a
    /// working-tree change; it is deleted along with this instance.</para>
    /// </remarks>
    public string AddBareRemote(string name = "origin")
    {
        var path = Directory.CreateTempSubdirectory("bv-test-remote-").FullName;
        _ = Repository.Init(path, isBare: true);
        _remotePaths.Add(name, path);
        _ = _repository.Network.Remotes.Add(name, path);
        var branch = _repository.Head;
        _ = _repository.Branches.Update(
            branch,
            b => b.Remote = name,
            b => b.UpstreamBranch = branch.CanonicalName);
        return path;
    }

    /// <summary>
    /// Sets the committer identity in the repository's local configuration.
    /// </summary>
    /// <param name="name">The display name of the committer.</param>
    /// <param name="email">The email address of the committer.</param>
    public void SetCommitterIdentity(string name, string email)
    {
        _repository.Config.Set("user.name", name);
        _repository.Config.Set("user.email", email);
    }

    /// <summary>
    /// Creates a lightweight tag on the current <c>HEAD</c> commit.
    /// </summary>
    /// <param name="name">The name of the tag.</param>
    public void CreateTag(string name) => _ = _repository.ApplyTag(name);

    /// <summary>
    /// Gets the most recent commits reachable from <c>HEAD</c>, newest first.
    /// </summary>
    /// <param name="count">The maximum number of commits to return.</param>
    /// <returns>The commits, newest first. Fewer than <paramref name="count"/> commits are returned
    /// if the history is shorter.</returns>
    /// <remarks>
    /// <para>Commits are sorted topologically, not by time: commits made in the same second — the norm for
    /// commits a test makes, and for those made by the code it exercises — carry the same timestamp, and
    /// the default time-based sort puts them in an arbitrary order.</para>
    /// </remarks>
    public IReadOnlyList<TempGitCommit> GetCommits(int count)
    {
        using var repository = new Repository(RootPath);
        var filter = new CommitFilter
        {
            IncludeReachableFrom = repository.Head,
            SortBy = CommitSortStrategies.Topological,
        };
        var result = new List<TempGitCommit>(count);
        foreach (var commit in repository.Commits.QueryBy(filter).Take(count))
        {
            result.Add(DescribeCommit(repository, commit));
        }

        return result;
    }

    /// <summary>
    /// Gets the SHA of the tip of the branch homonymous to the current one on a remote added by
    /// <see cref="AddBareRemote"/>.
    /// </summary>
    /// <param name="name">The name of the remote.</param>
    /// <returns>The SHA of the remote branch's tip, or <see langword="null"/> if the remote has no such
    /// branch, i.e. if nothing has been pushed to it yet.</returns>
    public string? GetRemoteTipSha(string name = "origin")
    {
        using var remote = new Repository(_remotePaths[name]);
        return remote.Branches[CurrentBranchName]?.Tip?.Sha;
    }

    /// <summary>
    /// Stages all changes and creates a commit, which may be empty.
    /// </summary>
    /// <param name="message">The commit message.</param>
    public void CommitAll(string message = "Test commit")
    {
        Commands.Stage(_repository, "*");
        var signature = new Signature("Test", "test@example.com", DateTimeOffset.Now);
        _ = _repository.Commit(message, signature, signature, new CommitOptions { AllowEmptyCommit = true });
    }

    /// <summary>
    /// Creates a new branch at the current <c>HEAD</c> commit and checks it out.
    /// </summary>
    /// <param name="name">The name of the new branch.</param>
    public void CheckoutNewBranch(string name)
    {
        var branch = _repository.CreateBranch(name);
        _ = Commands.Checkout(_repository, branch);
    }

    /// <summary>
    /// Checks out an existing branch.
    /// </summary>
    /// <param name="name">The name of the branch.</param>
    public void Checkout(string name) => _ = Commands.Checkout(_repository, _repository.Branches[name]);

    /// <summary>
    /// Checks out the current <c>HEAD</c> commit directly, detaching <c>HEAD</c> from any branch.
    /// </summary>
    public void CheckoutDetached() => _ = Commands.Checkout(_repository, _repository.Head.Tip);

    /// <summary>
    /// Merges a branch into the current branch, always creating a merge commit.
    /// </summary>
    /// <param name="branchName">The name of the branch to merge.</param>
    public void Merge(string branchName)
    {
        var signature = new Signature("Test", "test@example.com", DateTimeOffset.Now);
        var options = new MergeOptions { FastForwardStrategy = FastForwardStrategy.NoFastForward };
        _ = _repository.Merge(_repository.Branches[branchName], signature, options);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            DeleteDirectory(RootPath);
        }
        finally
        {
            // Remotes live in directories of their own, so a working tree that refuses to go — a stray lock
            // on a Git object file is the classic — must not take them with it.
            foreach (var path in _remotePaths.Values)
            {
                DeleteDirectory(path);
            }
        }
    }

    private static TempGitCommit DescribeCommit(Repository repository, Commit commit)
    {
        // A root commit has no parent to compare against; libgit2 takes a null tree to mean "the empty tree".
        var parentTree = commit.Parents.FirstOrDefault()?.Tree;
        var changes = repository.Diff.Compare<TreeChanges>(parentTree, commit.Tree);
        return new(
            commit.Sha,
            commit.Message.TrimEnd('\n'),
            commit.Committer.Name,
            commit.Committer.Email,
            [.. changes.Select(x => x.Path).Order(StringComparer.Ordinal)]);
    }

    private static void DeleteDirectory(string path)
    {
        // Git object files are read-only; clear attributes so the recursive delete succeeds on Windows.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
