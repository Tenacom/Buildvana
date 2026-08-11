// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using CommunityToolkit.Diagnostics;
using Louis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters;

/// <summary>
/// Represents a release in a server-independent way.
/// </summary>
internal abstract partial class ServerRelease : IAsyncDisposable
{
    // The message the release commit carries between being made and being named after the version
    // computed from it. See NameReleaseCommit.
    private const string ProvisionalMessage = "Prepare release [skip ci]";

    private readonly IReporter _reporter;
    private readonly GitService _git;
    private readonly VersionService _version;
    private readonly Stack<Func<ValueTask>> _rollbackActions = new();
    private readonly List<AssetData> _assets = [];

    private bool _published;
    private bool _repositoryUpdated;
    private int _postReleaseCommits;
    private bool _updatesPushed;

    private protected ServerRelease(IServiceProvider services)
    {
        Guard.IsNotNull(services);

        _reporter = services.GetRequiredService<IReporter>();
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
    protected string ReleaseCommitSha { get; private set; } = string.Empty;

    /// <summary>
    /// Ensures that a "Prepare release" commit exists, creating an empty one if necessary.
    /// </summary>
    /// <remarks>
    /// <para>The first call creates an empty commit, refreshes version information from the new Git height,
    /// then amends the commit with the final version-bearing message and captures its SHA into
    /// <see cref="ReleaseCommitSha"/>. Subsequent calls are no-ops.</para>
    /// <para><see cref="UpdateRepository"/> calls this implicitly, because it amends the release commit and
    /// builds its own message afterwards. <see cref="AddPostReleaseCommit"/> does not: it requires a release
    /// commit to exist already, so that the version cannot move under a message its caller has formatted.</para>
    /// <para>Call this directly to settle the version before anything reads it - notably before building, so
    /// that artifacts carry the same version that will be tagged and published.</para>
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

        _reporter.Info("Creating release commit...");
        _git.Commit(ProvisionalMessage, allowEmpty: true);
        _repositoryUpdated = true;
        NameReleaseCommit();

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

    public void UpdateRepository(params string[] files)
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

        // Staging comes first, so that the files are already in the index when the release commit is
        // made: the version is computed from what the commit contains, and these files are part of it.
        _git.Stage(files);
        EnsureReleaseCommit();
        _reporter.Info("Amending release commit...");
        _git.Commit(ProvisionalMessage, amend: true, allowEmpty: true);
        NameReleaseCommit();
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
    /// <para>A release commit must already exist: call <see cref="EnsureReleaseCommit"/> (directly or via
    /// <see cref="UpdateRepository"/>) first.</para>
    /// <para>This method deliberately does not create the release commit itself. Doing so would move the
    /// Git height, and with it the version, in the middle of a call whose <paramref name="message"/> the
    /// caller has already formatted - typically from that very version, which the message would then
    /// misreport. Requiring the commit up front keeps the version settled before any caller reads it.</para>
    /// </remarks>
    public void AddPostReleaseCommit(string message, params string[] files)
    {
        Guard.IsNotNullOrEmpty(message);
        Guard.IsNotNull(files);
        EnsurePending();

        if (_updatesPushed)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: cannot add a post-release commit when updates have already been pushed.");
        }

        if (!_repositoryUpdated)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: cannot add a post-release commit before the release commit has been created.");
        }

        _git.Stage(files);
        _reporter.Info("Committing post-release changed files...");
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
            _reporter.Info("Repository unchanged, no commit to push.");
            return;
        }

        _git.Push();
        _updatesPushed = true;

        // The rollback action is defined in EnsureReleaseCommit, because
        // commit and push can't be undone in reverse order (as rollback actions are processed):
        // first we need to undo the commits (a.k.a. reset), then force push to "undo" the push.
    }

    public void AddAsset(string path, string? description = null, string? mimeType = null)
    {
        EnsurePending();
        Guard.IsNotNullOrEmpty(path);

        if (string.IsNullOrEmpty(description))
        {
            description = Path.GetFileName(path);
        }

        if (string.IsNullOrEmpty(mimeType))
        {
            mimeType = "application/octet-stream";
        }

        _assets.Add(new(path, description, mimeType));
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
                _reporter.Warning($"{ex.GetType().Name} in release rollback action: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
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

    /// <summary>
    /// Refreshes the version from the release commit as it now stands, then rewrites the commit's message
    /// with it and recaptures <see cref="ReleaseCommitSha"/>.
    /// </summary>
    /// <remarks>
    /// <para>The order is forced from both ends. The Git height, and with it the version, is computed from
    /// <em>committed</em> content — the index and the working tree are invisible to it — so the version can
    /// only be read once the commit's tree is final. The message quotes the version, so it can only be
    /// written once the version has been read. Hence the release commit is always made first under
    /// <see cref="ProvisionalMessage"/> and named afterwards, by this method, and every method that changes
    /// the commit's tree ends by calling it.</para>
    /// <para>Skipping the refresh here is what made a version-spec bump publish a version one patch below
    /// the one its own artifacts were built with: the version file reached the commit after the height had
    /// been computed, so the new version line looked as if it had no committed history at all.</para>
    /// </remarks>
    private void NameReleaseCommit()
    {
        var previousVersion = _version.CurrentStr;
        _version.Update();
        if (!string.Equals(previousVersion, _version.CurrentStr, StringComparison.Ordinal))
        {
            _reporter.Info($"Version changed to {_version.CurrentStr}");
        }

        _git.Commit($"Prepare release {_version.CurrentStr} [skip ci]", amend: true, allowEmpty: true);
        ReleaseCommitSha = _git.HeadSha;
    }
}
