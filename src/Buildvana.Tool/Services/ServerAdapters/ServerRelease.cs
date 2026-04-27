// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using Cake.Common.Diagnostics;
using Cake.Core;
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
    private readonly ICakeContext _context;
    private readonly GitService _git;
    private readonly VersionService _version;
    private readonly Stack<Func<ValueTask>> _rollbackActions = new();
    private readonly List<AssetData> _assets = [];

    private bool _published;
    private bool _repositoryUpdated;
    private bool _updatesPushed;

    private protected ServerRelease(IServiceProvider services)
    {
        Guard.IsNotNull(services);

        _context = services.GetRequiredService<ICakeContext>();
        _git = services.GetRequiredService<GitService>();
        _version = services.GetRequiredService<VersionService>();
    }

    public bool IsDisposed { get; private set; }

    public void UpdateRepository(params FilePath[] files)
    {
        Guard.IsNotNull(files);
        EnsurePending();

        if (_updatesPushed)
        {
            ThrowHelper.ThrowInvalidOperationException("Internal error: cannot update repository when updates have already been pushed.");
        }

        _git.Stage(files);

        // If this was the first call to this method, the commit changed the Git height;
        // update version information and amend the commit adding the correct version.
        // Amending a commit does not further change the Git height.
        if (_repositoryUpdated)
        {
            _context.Information("Amending commit...");
            _git.Commit($"Prepare release {_version.CurrentStr} [skip ci]", true);
        }
        else
        {
            _context.Information("Committing changed files...");
            _git.Commit("Prepare release [skip ci]");

            // Git height has changed
            _version.Update();
            _context.Information($"Version changed to {_version.CurrentStr}");
            _git.Commit($"Prepare release {_version.CurrentStr} [skip ci]", true);
            _repositoryUpdated = true;
            OnRollback(() =>
            {
                // This lambda rolls back both UpdateRepository and (if appropriate) PushUpdates.
                // First, "undo" the commit by resetting the local repository.
                _git.UndoLastCommit();

                // If updates have already been pushed...
                if (_updatesPushed)
                {
                    // "Undo" the push by force pushing the previous commit
                    // (to which we have just reset).
                    _git.Push(force: true);
                }
            });
        }
    }

    public void PushUpdates()
    {
        EnsurePending();

        if (!_repositoryUpdated)
        {
            _context.Information("Repository unchanged, no commit to push.");
            return;
        }

        _git.Push();
        _updatesPushed = true;

        // The rollback action is defined in UpdateRepository, because
        // commit and push can't be undone in reverse order (as rollback actions are processed):
        // first we need to undo the commit (a.k.a. reset), then force push to "undo" the push.
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
                _context.Warning($"{ex.GetType().Name} in release rollback action: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
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
