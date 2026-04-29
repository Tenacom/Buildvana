// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Services.Versioning;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Octokit;

using SysPath = System.IO.Path;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// ServerRelease implementation for GitHub.
/// </summary>
internal sealed class GitHubServerRelease : ServerRelease
{
    private readonly GitHubServerAdapter _server;
    private readonly IBuildHost _host;
    private readonly VersionService _version;
    private readonly Release _gitHubRelease;

    private bool _gitHubReleaseDeleted;

    private GitHubServerRelease(GitHubServerAdapter server, IServiceProvider services, Release gitHubRelease)
        : base(services)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);
        Guard.IsNotNull(gitHubRelease);

        _server = server;
        _host = services.GetRequiredService<IBuildHost>();
        _version = services.GetRequiredService<VersionService>();
        _gitHubRelease = gitHubRelease;

        OnRollback(async () =>
        {
            // Do this only if the release has not been previously deleted by rolling back its publication
            if (!_gitHubReleaseDeleted)
            {
                await _server.DeleteReleaseAsync(_gitHubRelease, null).ConfigureAwait(false);
            }
        });
    }

    public static async Task<GitHubServerRelease> CreateAsync(GitHubServerAdapter server, IServiceProvider services, Func<Task<Release>> createGitHubReleaseAsync)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);
        Guard.IsNotNull(createGitHubReleaseAsync);

        var gitHubRelease = await createGitHubReleaseAsync().ConfigureAwait(false);
        return new(server, services, gitHubRelease);
    }

    protected override async Task DoPublishAsync(IReadOnlyList<AssetData> assets)
    {
        var assetCount = assets.Count;
        if (assetCount > 0)
        {
            var i = 0;
            foreach (var asset in assets)
            {
                i++;
                _host.LogInformation($"Uploading asset {i} of {assetCount}: {SysPath.GetFileName(asset.Path)} ({asset.Description})...");
                await _server.UploadReleaseAssetAsync(_gitHubRelease, asset.Path, asset.MimeType, asset.Description).ConfigureAwait(false);
            }
        }
        else
        {
            _host.LogInformation("Asset upload skipped: no release assets defined.");
        }

        await _server.PublishReleaseAsync(_gitHubRelease, ReleaseCommitSha).ConfigureAwait(false);
    }

    protected override async Task UndoPublishAsync()
    {
        // Delete the release and the created tag
        await _server.DeleteReleaseAsync(_gitHubRelease, _version.CurrentStr).ConfigureAwait(false);

        // Prevent the last rollback action from trying to delete the release again
        _gitHubReleaseDeleted = true;
    }

    protected override Task OnPublishedAsync()
    {
        _server.SetActionsStepOutput("version", _version.CurrentStr);
        return Task.CompletedTask;
    }
}
