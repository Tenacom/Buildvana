// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using LibGit2Sharp;

namespace Buildvana.Core.Testing;

/// <summary>
/// A disposable real Git repository in a temporary directory, for tests exercising Git-dependent code.
/// </summary>
public sealed class TempGitRepo : IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Repository _repository;

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
    public string CurrentBranchName => _repository.Head.FriendlyName;

    /// <summary>
    /// Gets the SHA of the current <c>HEAD</c> commit.
    /// </summary>
    public string HeadSha => _repository.Head.Tip.Sha;

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
        DeleteDirectory(RootPath);
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
