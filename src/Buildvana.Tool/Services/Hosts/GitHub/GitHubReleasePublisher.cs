// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.IO;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Release;
using Buildvana.Tool.Services.Versioning;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts.GitHub;

/// <summary>
/// The release publisher of a <see cref="GitHubHost"/>: a draft release created first, then published.
/// </summary>
internal sealed class GitHubReleasePublisher : ReleasePublisher
{
    private readonly GitHubHost _host;
    private readonly IReporter _reporter;
    private readonly VersionService _version;
    private readonly IGitService _git;

    private GitHubRelease? _release;
    private string? _publishedTag;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubReleasePublisher"/> class.
    /// </summary>
    /// <param name="host">The host to publish on.</param>
    /// <param name="reporter">The reporter to report to.</param>
    /// <param name="version">The version service.</param>
    /// <param name="git">The Git service.</param>
    public GitHubReleasePublisher(GitHubHost host, IReporter reporter, VersionService version, IGitService git)
    {
        Guard.IsNotNull(host);
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(version);
        Guard.IsNotNull(git);
        _host = host;
        _reporter = reporter;
        _version = version;
        _git = git;
    }

    /// <inheritdoc/>
    /// <exception cref="BuildFailedException">Two assets have the same file name.</exception>
    /// <remarks>
    /// <para>GitHub names an asset after its file, and refuses two assets of a release with the same name.</para>
    /// </remarks>
    public override void CheckAssets(IReadOnlyList<ReleaseAsset> assets)
    {
        Guard.IsNotNull(assets);
        var problems = Duplicates(assets.Select(static x => Path.GetFileName(x.Path)))
            .Select(static x => $"More than one asset has the file name '{x}'.")
            .ToList();

        BuildFailedException.ThrowIf(
            problems.Count > 0,
            "GitHub refuses the release assets:" + string.Concat(problems.Select(static x => "\n- " + x)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>The draft is named after the version as it stands before the release commit, which may still move it.</para>
    /// </remarks>
    public override async Task BeginAsync()
    {
        var tag = _version.CurrentStr;
        _reporter.Info("Creating a provisional draft release...");
        var newRelease = new GitHubNewRelease {
            TagName = tag,
            Name = $"{tag} [provisional]",
            TargetCommitish = _git.CurrentBranch,
            Prerelease = _version.IsPrerelease,
            Draft = true,
        };

        _release = await _host.Client.CreateReleaseAsync(_host.Owner, _host.Repository, newRelease).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task PublishAsync(string tag, string sha, IReadOnlyList<ReleaseAsset> assets)
    {
        Guard.IsNotNullOrEmpty(tag);
        Guard.IsNotNullOrEmpty(sha);
        Guard.IsNotNull(assets);
        var release = _release ?? ThrowHelper.ThrowInvalidOperationException<GitHubRelease>(
            "Internal error: cannot publish a release before beginning it.");

        var assetCount = assets.Count;
        if (assetCount == 0)
        {
            _reporter.Info("Asset upload skipped: no release assets defined.");
        }

        for (var i = 0; i < assetCount; i++)
        {
            var asset = assets[i];
            _reporter.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"Uploading asset {i + 1} of {assetCount}: {Path.GetFileName(asset.Path)} ({asset.Description})..."));
            await UploadAssetAsync(release, asset).ConfigureAwait(false);
        }

        _reporter.Info($"Generating release notes for {tag}...");
        var notesRequest = new GitHubReleaseNotesRequest { TagName = tag, TargetCommitish = sha };
        var notes = await _host.Client.GenerateReleaseNotesAsync(_host.Owner, _host.Repository, notesRequest).ConfigureAwait(false);
        var changelogUrl = _host.GetFileUrl("CHANGELOG.md", GitRef.Branch(_git.CurrentBranch));
        var body = $"We also have a [human-curated changelog]({changelogUrl}).\n\n---\n\n" + notes.Body;

        _reporter.Info($"Publishing the provisional draft release as {tag} (target {sha})...");
        var update = new GitHubReleaseUpdate {
            TagName = tag,
            Name = tag,
            TargetCommitish = sha,
            Body = body,
            Prerelease = _version.IsPrerelease,
            Draft = false,
        };

        await _host.Client.UpdateReleaseAsync(_host.Owner, _host.Repository, release.Id, update).ConfigureAwait(false);
        _publishedTag = tag;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>A deleted release is named after the tag it was published under. A draft carries a provisional name,
    /// which the release commit may have made wrong, so it is reported as the draft it is.</para>
    /// </remarks>
    public override async Task UndoAsync()
    {
        if (_release is null)
        {
            return;
        }

        _reporter.Info("Deleting the previously created release...");
        await _host.Client.DeleteReleaseAsync(_host.Owner, _host.Repository, _release.Id).ConfigureAwait(false);
        _reporter.Notice(_publishedTag is null ? "Deleted the provisional draft release." : $"Deleted release {_publishedTag}.");
        _release = null;
    }

    private async Task UploadAssetAsync(GitHubRelease release, ReleaseAsset asset)
    {
        _reporter.Detail($"Uploading asset {asset.Path}...");
        var contents = UserFile.OpenRead(asset.Path);
        await using (contents.ConfigureAwait(false))
        {
            await _host.Client.UploadReleaseAssetAsync(
                release,
                Path.GetFileName(asset.Path),
                asset.MimeType,
                string.IsNullOrEmpty(asset.Description) ? null : asset.Description,
                contents).ConfigureAwait(false);
        }
    }
}
