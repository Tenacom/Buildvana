// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;
using Louis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters;

/// <summary>
/// Represents a release in a server-independent way.
/// </summary>
public abstract partial class ServerRelease : IAsyncDisposable
{
    private readonly IBuildHost _host;
    private readonly GitService _git;
    private readonly VersionService _version;
    private readonly Stack<Func<ValueTask>> _rollbackActions = new();
    private readonly List<AssetData> _assets = [];

    private bool _published;
    private bool _repositoryUpdated;
    private string _releaseCommitSha = string.Empty;
    private int _postReleaseCommits;
    private bool _updatesPushed;

    private protected ServerRelease(IServiceProvider services)
    {
        Guard.IsNotNull(services);

        _host = services.GetRequiredService<IBuildHost>();
        _git = services.GetRequiredService<GitService>();
        _version = services.GetRequiredService<VersionService>();
    }

    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the SHA of the "Prepare release" commit, once it has been created.
    /// </summary>
    /// <remarks>
    /// <para>This is the commit the release tag should point to, regardless of any post-release commits
    /// (e.g. self-reference dogfooding) that may be pushed on top of it.</para>
    /// <para>Returns the empty string before <see cref="EnsureReleaseCommit"/> (or any method that calls it,
    /// such as <see cref="UpdateRepository"/>) has run.</para>
    /// </remarks>
    protected string ReleaseCommitSha => _releaseCommitSha;

    /// <summary>
    /// Ensures that a "Prepare release" commit exists, creating an empty one if necessary.
    /// </summary>
    /// <remarks>
    /// <para>The first call creates an empty commit, refreshes version information from the new Git height,
    /// then amends the commit with the final version-bearing message and captures its SHA into
    /// <see cref="ReleaseCommitSha"/>. Subsequent calls are no-ops.</para>
    /// <para><see cref="UpdateRepository"/> calls this implicitly; callers only need to invoke it directly
    /// when they want to guarantee a release commit exists without staging any file.</para>
    /// </remarks>
    public void EnsureReleaseCommit()
    {
        EnsurePending();

        if (_updatesPushed)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: cannot create the release commit when updates have already been pushed.");
        }

        if (_repositoryUpdated)
        {
            return;
        }

        _host.LogInformation("Creating release commit...");
        _git.Commit("Prepare release [skip ci]", allowEmpty: true);

        // Git height has changed
        _version.Update();
        _host.LogInformation($"Version changed to {_version.CurrentStr}");
        _git.Commit($"Prepare release {_version.CurrentStr} [skip ci]", amend: true, allowEmpty: true);
        _repositoryUpdated = true;
        _releaseCommitSha = _git.HeadSha;

        OnRollback(() =>
        {
            // This lambda rolls back the release commit, any post-release commits added on top,
            // and (if appropriate) the push that carried them to the remote.
            var commitsToUndo = 1 + _postReleaseCommits;
            for (var i = 0; i < commitsToUndo; i++)
            {
                _git.UndoLastCommit();
            }

            // If updates have already been pushed...
            if (_updatesPushed)
            {
                // "Undo" the push by force pushing the previous commit
                // (to which we have just reset).
                _git.Push(force: true);
            }
        });
    }

    public void UpdateRepository(params FilePath[] files)
    {
        Guard.IsNotNull(files);
        EnsurePending();

        if (_updatesPushed)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: cannot update repository when updates have already been pushed.");
        }

        if (_postReleaseCommits > 0)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: cannot update the release commit after a post-release commit has been added.");
        }

        EnsureReleaseCommit();

        _git.Stage(files);
        _host.LogInformation("Amending release commit...");
        _git.Commit($"Prepare release {_version.CurrentStr} [skip ci]", amend: true, allowEmpty: true);
        _releaseCommitSha = _git.HeadSha;
    }

    /// <summary>
    /// Adds a separate commit on top of the release commit, e.g. for post-release dogfooding updates
    /// whose contents reference the just-published version and therefore must not be part of the tagged commit.
    /// </summary>
    /// <param name="message">The commit message. Should include <c>[skip ci]</c> if the new commit's state
    /// is not yet buildable on the branch tip (for example, because it references packages that haven't
    /// been published to the feed yet).</param>
    /// <param name="files">The paths of the files to stage into the new commit.</param>
    /// <remarks>
    /// If no release commit has been created yet, this method calls <see cref="EnsureReleaseCommit"/>
    /// first, so the post-release commit always sits on top of a tagged release commit (possibly empty).
    /// </remarks>
    public void AddPostReleaseCommit(string message, params FilePath[] files)
    {
        Guard.IsNotNullOrEmpty(message);
        Guard.IsNotNull(files);
        EnsurePending();

        if (_updatesPushed)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: cannot add a post-release commit when updates have already been pushed.");
        }

        EnsureReleaseCommit();

        _git.Stage(files);
        _host.LogInformation("Committing post-release changed files...");
        _git.Commit(message);
        _postReleaseCommits++;

        // No rollback registered here on purpose: the rollback installed by EnsureReleaseCommit
        // walks back 1 + _postReleaseCommits commits, so post-release commits are covered by
        // a single, ordered rollback rather than per-commit rollbacks popped LIFO.
    }

    public void PushUpdates()
    {
        EnsurePending();

        if (!_repositoryUpdated)
        {
            _host.LogInformation("Repository unchanged, no commit to push.");
            return;
        }

        _git.Push();
        _updatesPushed = true;

        // The rollback action is defined in EnsureReleaseCommit, because
        // commit and push can't be undone in reverse order (as rollback actions are processed):
        // first we need to undo the commits (a.k.a. reset), then force push to "undo" the push.
    }

    public void AddAsset(FilePath path, string? description = null, string? mimeType = null)
    {
        EnsurePending();
        Guard.IsNotNull(path);

        if (string.IsNullOrEmpty(description))
        {
            description = path.GetFilename().ToString();
        }

        if (string.IsNullOrEmpty(mimeType))
        {
            mimeType = "application/octet-stream";
        }

        _assets.Add(new(path.FullPath, description, mimeType));
    }

    public async Task PublishAsync()
    {
        EnsurePending();

        await DoPublishAsync(_assets).ConfigureAwait(false);
        OnRollback(async () => await UndoPublishAsync().ConfigureAwait(false));
        await OnPublishedAsync().ConfigureAwait(false);
        _published = true;
        _rollbackActions.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed || _published)
        {
            return;
        }

        IsDisposed = true;
        while (_rollbackActions.TryPop(out var rollbackAction))
        {
            try
            {
                await rollbackAction().ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCriticalError())
            {
                _host.LogWarning($"{ex.GetType().Name} in release rollback action: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
        }

        _rollbackActions.Clear();
        _git.Dispose();
    }

    protected abstract Task DoPublishAsync(IReadOnlyList<AssetData> assets);

    protected abstract Task UndoPublishAsync();

    protected virtual Task OnPublishedAsync() => Task.CompletedTask;

    protected void OnRollback(Action action) => OnRollback(() =>
    {
        action();
        return ValueTask.CompletedTask;
    });

    protected void OnRollback(Func<ValueTask> actionAsync)
    {
        EnsurePending();
        _rollbackActions.Push(actionAsync);
    }

    protected void EnsurePending()
    {
        if (IsDisposed)
        {
            ThrowHelper.ThrowObjectDisposedException(GetType().Name);
        }

        if (_published)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: release has already been published.");
        }
    }
}
