// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Tool.Services.Platforms;
using Buildvana.Tool.Services.Versioning;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// ServerRelease implementation for GitHub.
/// </summary>
internal sealed class GitHubServerRelease : ServerRelease
{
    private readonly GitHubServerAdapter _server;
    private readonly IReporter _reporter;
    private readonly VersionService _version;
    private readonly GitHubRelease _gitHubRelease;
    private readonly StepOutputWriter? _stepOutputs;

    private bool _gitHubReleaseDeleted;

    private GitHubServerRelease(
        GitHubServerAdapter server,
        IServiceProvider services,
        GitHubRelease gitHubRelease,
        StepOutputWriter? stepOutputs)
        : base(services)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);
        Guard.IsNotNull(gitHubRelease);

        _server = server;
        _reporter = services.GetRequiredService<IReporter>();
        _version = services.GetRequiredService<VersionService>();
        _gitHubRelease = gitHubRelease;
        _stepOutputs = stepOutputs;

        OnRollback(async () => {
            // Do this only if the release has not been previously deleted by rolling back its publication
            if (!_gitHubReleaseDeleted)
            {
                await _server.DeleteReleaseAsync(_gitHubRelease, null).ConfigureAwait(false);
            }
        });
    }

    public static async Task<GitHubServerRelease> CreateAsync(
        GitHubServerAdapter server,
        IServiceProvider services,
        Func<Task<GitHubRelease>> createGitHubReleaseAsync)
    {
        Guard.IsNotNull(server);
        Guard.IsNotNull(services);
        Guard.IsNotNull(createGitHubReleaseAsync);

        // The step outputs are opened here, before the draft release exists, and kept for the whole
        // life of the release. Opening them where they are used - after publication, to write the step
        // output - would let a variable that was never set fail a release that had otherwise
        // succeeded, and roll back everything it had just done.
        var platform = services.GetRequiredService<CIPlatform>();
        var stepOutputs = platform.HasStepOutputs ? platform.OpenStepOutputs() : null;
        var gitHubRelease = await createGitHubReleaseAsync().ConfigureAwait(false);
        return new(server, services, gitHubRelease, stepOutputs);
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
                _reporter.Info(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Uploading asset {i} of {assetCount}: {Path.GetFileName(asset.Path)} ({asset.Description})..."));
                await _server.UploadReleaseAssetAsync(_gitHubRelease, asset.Path, asset.MimeType, asset.Description).ConfigureAwait(false);
            }
        }
        else
        {
            _reporter.Info("Asset upload skipped: no release assets defined.");
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
        _stepOutputs?.Write("version", _version.CurrentStr);
        return Task.CompletedTask;
    }
}
