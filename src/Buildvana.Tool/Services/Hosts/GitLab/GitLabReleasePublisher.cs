// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
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
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The release publisher of a <see cref="GitLabHost"/>: the assets uploaded to a generic package, then a release
/// that links them.
/// </summary>
/// <remarks>
/// <para>GitLab has no draft releases, so nothing exists on the host before <see cref="PublishAsync"/>.</para>
/// <para>The generic package is named after the repository, and its version is the release tag.</para>
/// </remarks>
internal sealed class GitLabReleasePublisher : ReleasePublisher
{
    // The Maintainer role. Deleting a package needs it, and a rollback deletes the package.
    private const int MaintainerAccessLevel = 40;

    private readonly GitLabHost _host;
    private readonly IReporter _reporter;
    private readonly IGitService _git;

    private string? _releaseTag;
    private long? _packageId;
    private string? _packageVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLabReleasePublisher"/> class.
    /// </summary>
    /// <param name="host">The host to publish on.</param>
    /// <param name="reporter">The reporter to report to.</param>
    /// <param name="git">The Git service.</param>
    public GitLabReleasePublisher(GitLabHost host, IReporter reporter, IGitService git)
    {
        Guard.IsNotNull(host);
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(git);
        _host = host;
        _reporter = reporter;
        _git = git;
    }

    /// <summary>
    /// Checks that GitLab accepts a list of release assets, and fails the build when it does not.
    /// </summary>
    /// <param name="assets">The release assets.</param>
    /// <exception cref="BuildFailedException">A file name is not valid in the generic package registry, or as the
    /// direct asset path of a release link; or two assets have the same file name, or the same description.</exception>
    /// <remarks>
    /// <para>A release link takes its name from the description of the asset, and GitLab refuses two links of a
    /// release with the same name or the same direct asset path.</para>
    /// </remarks>
    public static void CheckAssets(IReadOnlyList<ReleaseAsset> assets)
    {
        Guard.IsNotNull(assets);
        var problems = new List<string>();
        var fileNames = assets.Select(static x => Path.GetFileName(x.Path)).ToArray();
        foreach (var fileName in fileNames.Distinct(StringComparer.Ordinal))
        {
            if (!GitLabApiClient.IsValidPackageFileName(fileName))
            {
                problems.Add($"'{fileName}' is not a valid file name in the generic package registry.");
            }
            else if (!GitLabApiClient.IsValidDirectAssetPath("/" + fileName))
            {
                problems.Add($"'{fileName}' is not a valid file name for a release link.");
            }
        }

        foreach (var fileName in Duplicates(fileNames))
        {
            problems.Add($"More than one asset has the file name '{fileName}'.");
        }

        foreach (var description in Duplicates(assets.Select(static x => x.Description)))
        {
            problems.Add($"More than one asset has the description '{description}'.");
        }

        BuildFailedException.ThrowIf(
            problems.Count > 0,
            "GitLab refuses the release assets:" + string.Concat(problems.Select(static x => "\n- " + x)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>This method creates nothing. It reads the releases of the project, which fails for a token that cannot
    /// reach them. With an access token, it also checks that the user of the token has the Maintainer role.</para>
    /// </remarks>
    public override async Task BeginAsync()
    {
        var project = _host.ProjectPath;
        _reporter.Info($"Checking access to the releases of {project}...");
        _ = await _host.Client.GetReleasesAsync(project, 1).ConfigureAwait(false);
        if (_host.TokenKind == GitLabTokenKind.JobToken)
        {
            // A job token has the access of the user who started the job, and cannot read a membership.
            _reporter.Info("Role check skipped: GitLab states no role for a job token.");
            return;
        }

        _reporter.Info("Checking the role of the token...");
        var user = await _host.Client.GetCurrentUserAsync().ConfigureAwait(false);
        var member = await _host.Client.GetProjectMemberAsync(project, user.Id).ConfigureAwait(false);

        // The releases of the project are readable, so a missing membership is not a missing project. A user
        // with no membership is an instance administrator or an organization owner, who can delete a package,
        // or a user without push access, whose push of the release commit fails before anything is published.
        if (member is null)
        {
            _reporter.Info("Role check skipped: the user of the token is not a member of the project.");
            return;
        }

        if (member.AccessLevel < MaintainerAccessLevel)
        {
            var level = member.AccessLevel.ToString(CultureInfo.InvariantCulture);
            throw new BuildFailedException(
                $"The GitLab token has access level {level} on {project}, below the Maintainer role. "
                + "A release needs the Maintainer role, because a rollback deletes the generic package.");
        }
    }

    /// <inheritdoc/>
    public override async Task PublishAsync(string tag, string sha, IReadOnlyList<ReleaseAsset> assets)
    {
        Guard.IsNotNullOrEmpty(tag);
        Guard.IsNotNullOrEmpty(sha);
        Guard.IsNotNull(assets);
        CheckAssets(assets);

        var assetCount = assets.Count;
        if (assetCount == 0)
        {
            _reporter.Info("Asset upload skipped: no release assets defined.");
        }

        var links = new List<GitLabReleaseLink>(assetCount);
        for (var i = 0; i < assetCount; i++)
        {
            var asset = assets[i];
            _reporter.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"Uploading asset {i + 1} of {assetCount}: {Path.GetFileName(asset.Path)} ({asset.Description})..."));
            links.Add(await UploadAssetAsync(tag, asset).ConfigureAwait(false));
        }

        var changelogUrl = _host.GetFileUrl("CHANGELOG.md", GitRef.Branch(_git.CurrentBranch));
        _reporter.Info($"Creating release {tag}...");
        var release = new GitLabNewRelease {
            TagName = tag,
            Name = tag,
            Description = $"We have a [human-curated changelog]({changelogUrl}).",
            Assets = new GitLabReleaseAssets { Links = links },
        };

        _ = await _host.Client.CreateReleaseAsync(_host.ProjectPath, release).ConfigureAwait(false);
        _releaseTag = tag;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>The release goes first, because its links point at the files of the package.</para>
    /// </remarks>
    public override async Task UndoAsync()
    {
        if (_releaseTag is { } tag)
        {
            _reporter.Info("Deleting the previously created release...");
            await _host.Client.DeleteReleaseAsync(_host.ProjectPath, tag).ConfigureAwait(false);
            _reporter.Notice($"Deleted release {tag}.");
            _releaseTag = null;
        }

        if (_packageId is { } packageId)
        {
            _reporter.Info("Deleting the previously created generic package...");
            await _host.Client.DeletePackageAsync(_host.ProjectPath, packageId).ConfigureAwait(false);
            _reporter.Notice($"Deleted generic package {_host.Repository} {_packageVersion}.");
            _packageId = null;
        }
    }

    private static IEnumerable<string> Duplicates(IEnumerable<string> values)
        => values.GroupBy(static x => x, StringComparer.Ordinal).Where(static x => x.Count() > 1).Select(static x => x.Key);

    private async Task<GitLabReleaseLink> UploadAssetAsync(string tag, ReleaseAsset asset)
    {
        _reporter.Detail($"Uploading asset {asset.Path}...");
        var project = _host.ProjectPath;
        var packageName = _host.Repository;
        var fileName = Path.GetFileName(asset.Path);
        var contents = UserFile.OpenRead(asset.Path);
        await using (contents.ConfigureAwait(false))
        {
            var file = await _host.Client.UploadPackageFileAsync(project, packageName, tag, fileName, contents)
                .ConfigureAwait(false);
            _packageId ??= file.PackageId;
            _packageVersion = tag;
        }

        return new GitLabReleaseLink {
            Name = asset.Description,
            Url = _host.Client.GetPackageFileUrl(project, packageName, tag, fileName).AbsoluteUri,
            DirectAssetPath = "/" + fileName,
            LinkType = "package",
        };
    }
}
