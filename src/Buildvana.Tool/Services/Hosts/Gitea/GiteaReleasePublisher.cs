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

namespace Buildvana.Tool.Services.Hosts.Gitea;

/// <summary>
/// The release publisher of a <see cref="GiteaHost"/>: a draft release on the pushed tag, its attachments, then
/// the publication of the draft.
/// </summary>
/// <remarks>
/// <para>Nothing exists on the host before <see cref="PublishAsync"/>. When Gitea processes the push of a tag, it
/// publishes a draft on the same tag name, so a draft created before the push would be published without its
/// attachments.</para>
/// <para>Gitea processes a tag push asynchronously, so a slow queue can still publish the draft before the
/// attachments are uploaded. The release ends complete either way.</para>
/// </remarks>
internal sealed class GiteaReleasePublisher : ReleasePublisher
{
    private readonly GiteaHost _host;
    private readonly IReporter _reporter;
    private readonly VersionService _version;
    private readonly IGitService _git;

    private (long Id, string Tag)? _release;
    private bool _published;

    /// <summary>
    /// Initializes a new instance of the <see cref="GiteaReleasePublisher"/> class.
    /// </summary>
    /// <param name="host">The host to publish on.</param>
    /// <param name="reporter">The reporter to report to.</param>
    /// <param name="version">The version service.</param>
    /// <param name="git">The Git service.</param>
    public GiteaReleasePublisher(GiteaHost host, IReporter reporter, VersionService version, IGitService git)
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
    /// <para>Gitea accepts two attachments of a release with the same name, but the download URL of an
    /// attachment names it by its file name, and then leads to one of them only.</para>
    /// </remarks>
    public override void CheckAssets(IReadOnlyList<ReleaseAsset> assets)
    {
        Guard.IsNotNull(assets);
        var problems = Duplicates(assets.Select(static x => Path.GetFileName(x.Path)))
            .Select(static x => $"More than one asset has the file name '{x}'.")
            .ToList();

        BuildFailedException.ThrowIf(
            problems.Count > 0,
            "The release assets cannot go to a Gitea release:" + string.Concat(problems.Select(static x => "\n- " + x)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>This method creates nothing. It reads the repository, which fails for a token that cannot reach it,
    /// and refuses a repository whose releases are disabled, or that the token cannot push to.</para>
    /// <para>Gitea does not state whether the token can write releases. A token without that access fails the
    /// release when <see cref="PublishAsync"/> creates the draft, and the rollback then deletes the tag.</para>
    /// </remarks>
    public override async Task BeginAsync()
    {
        var repositoryPath = $"{_host.Owner}/{_host.Repository}";
        _reporter.Info($"Checking access to {repositoryPath}...");
        var repository = await _host.Client.GetRepositoryAsync(_host.Owner, _host.Repository).ConfigureAwait(false);
        BuildFailedException.ThrowIfNot(
            repository.HasReleases,
            $"The releases of {repositoryPath} are disabled: enable them in the settings of the repository.");
        BuildFailedException.ThrowIfNot(
            repository.Permissions.Push,
            $"The Gitea token cannot push to {repositoryPath}. A release needs write access to the code of the repository.");
    }

    /// <inheritdoc/>
    public override async Task PublishAsync(string tag, string sha, IReadOnlyList<ReleaseAsset> assets)
    {
        Guard.IsNotNullOrEmpty(tag);
        Guard.IsNotNullOrEmpty(sha);
        Guard.IsNotNull(assets);
        _reporter.Info($"Creating a draft release on tag {tag}...");
        var newRelease = new GiteaNewRelease {
            TagName = tag,
            TargetCommitish = sha,
            Name = tag,
            Prerelease = _version.IsPrerelease,
            Draft = true,
        };

        var release = await _host.Client.CreateReleaseAsync(_host.Owner, _host.Repository, newRelease).ConfigureAwait(false);
        _release = (release.Id, tag);
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

        var changelogUrl = _host.GetFileUrl("CHANGELOG.md", GitRef.Branch(_git.CurrentBranch));
        _reporter.Info($"Publishing the draft release as {tag}...");
        var update = new GiteaReleaseUpdate {
            Name = tag,
            Body = $"We have a [human-curated changelog]({changelogUrl}).",
            Prerelease = _version.IsPrerelease,
            Draft = false,
        };

        await _host.Client.UpdateReleaseAsync(_host.Owner, _host.Repository, release.Id, update).ConfigureAwait(false);
        _published = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>A rollback deletes the release tag before this method runs. Gitea then keeps the release as a draft,
    /// and keeps a record of the tag once the release is deleted. This method deletes both.</para>
    /// <para>A release or a tag record that no longer exists is no failure, because there is nothing left to
    /// undo.</para>
    /// </remarks>
    public override async Task UndoAsync()
    {
        if (_release is not (var releaseId, var tag))
        {
            return;
        }

        _reporter.Info("Deleting the previously created release...");
        var deleted = await _host.Client.DeleteReleaseAsync(_host.Owner, _host.Repository, releaseId).ConfigureAwait(false);
        if (deleted)
        {
            _reporter.Notice(_published ? $"Deleted release {tag}." : $"Deleted the draft release on tag {tag}.");
        }
        else
        {
            _reporter.Info(_published ? $"Release {tag} not found." : $"Draft release on tag {tag} not found.");
        }

        _reporter.Info($"Deleting the record of tag {tag} on Gitea...");
        _ = await _host.Client.DeleteTagAsync(_host.Owner, _host.Repository, tag).ConfigureAwait(false);
        _release = null;
    }

    private async Task UploadAssetAsync(GiteaRelease release, ReleaseAsset asset)
    {
        _reporter.Detail($"Uploading asset {asset.Path}...");
        var contents = UserFile.OpenRead(asset.Path);
        await using (contents.ConfigureAwait(false))
        {
            await _host.Client.UploadReleaseAssetAsync(
                _host.Owner,
                _host.Repository,
                release.Id,
                Path.GetFileName(asset.Path),
                contents).ConfigureAwait(false);
        }
    }
}
