// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.IO;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// Continuous Integration adapter for GitHub.
/// </summary>
internal sealed class GitHubServerAdapter : ServerAdapter, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IReporter _reporter;
    private readonly VersionService _version;
    private readonly IGitService _git;
    private readonly GitHubRepositoryUrls _urls;
    private readonly string _token;
    private readonly GitHubApiClient _client;

    private GitHubServerAdapter(IServiceProvider services)
    {
        _services = services;
        _reporter = services.GetRequiredService<IReporter>();
        _version = services.GetRequiredService<VersionService>();
        _git = services.GetRequiredService<IGitService>();
        BuildFailedException.ThrowIfNot(
            GitUrlInfo.TryCreate(_git.OriginUrl, out var originInfo),
            $"Couldn't get information from origin URL '{_git.OriginUrl}'.");
        BuildFailedException.ThrowIfNot(originInfo.PathSegments.Count == 2, $"'{originInfo.Url}' is not a valid GitHub repository URL.");
        HostName = originInfo.Host;
        RepositoryOwner = originInfo.PathSegments[0];
        RepositoryName = originInfo.PathSegments[1];
        _urls = new GitHubRepositoryUrls(HostName, RepositoryOwner, RepositoryName);
        _token = RuntimeAccess.Translate(services.GetRequiredService<BuildvanaConfig>().Hosts.GitHub.GetToken);
        _client = new GitHubApiClient(HostName, _token, new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
    }

    /// <inheritdoc/>
    public override string Name => "GitHub Actions";

    /// <inheritdoc/>
    public override string HostName { get; }

    /// <inheritdoc/>
    public override string RepositoryOwner { get; }

    /// <inheritdoc/>
    public override string RepositoryName { get; }

    /// <inheritdoc/>
    public override Uri RepositoryUrl => _urls.Repository;

    /// <inheritdoc/>
    public override string PushUsername => "x-access-token";

    /// <inheritdoc/>
    // ReSharper disable once ConvertToAutoProperty // _token is also the REST API credential; keep it separate from the push contract
    public override string PushPassword => _token;

    /// <summary>
    /// Creates and returns an instance of <see cref="GitHubServerAdapter"/> if the build is running in a GitHub Actions runner.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>If the build is running in a GitHub Actions runner, a newly-created <see cref="GitHubServerAdapter"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static ServerAdapter? CreateIfApplicable(IServiceProvider services)
    {
        Guard.IsNotNull(services);
        return string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
            ? new GitHubServerAdapter(services)
            : null;
    }

    /// <inheritdoc/>
    public override async Task<bool> IsPrivateRepositoryAsync()
    {
        _reporter.Info("Fetching repository information...");
        var repository = await _client.GetRepositoryAsync(RepositoryOwner, RepositoryName).ConfigureAwait(false);
        return repository.Private;
    }

    /// <inheritdoc/>
    public override Uri GetReleaseUrl(string version) => _urls.ReleaseTag(version);

    /// <inheritdoc/>
    public override Uri GetFileUrl(string path, string commitish) => _urls.File(path, commitish);

    /// <inheritdoc/>
    public override async Task<ServerRelease> CreateReleaseAsync()
        => await GitHubServerRelease.CreateAsync(
            server: this,
            services: _services,
            createGitHubReleaseAsync: () => {
                // Create the release as a draft first, so if the token has no permissions we can bail out early
                var tag = _version.CurrentStr;
                _reporter.Info("Creating a provisional draft release...");
                var newRelease = new GitHubNewRelease {
                    TagName = tag,
                    Name = $"{tag} [provisional]",
                    TargetCommitish = _git.CurrentBranch,
                    Prerelease = _version.IsPrerelease,
                    Draft = true,
                };

                return _client.CreateReleaseAsync(RepositoryOwner, RepositoryName, newRelease);
            }).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously publishes a draft release on the GitHub repository.
    /// </summary>
    /// <param name="release">An object representing the GitHub release.</param>
    /// <param name="targetCommitish">The commit the release tag should point to. Pass the SHA of the
    /// release commit so the tag is anchored to it regardless of any post-release commits that may
    /// have been pushed on top of the branch.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task PublishReleaseAsync(GitHubRelease release, string targetCommitish)
    {
        Guard.IsNotNull(release);
        Guard.IsNotNullOrEmpty(targetCommitish);
        var tag = _version.CurrentStr;
        _reporter.Info($"Generating release notes for {tag}...");
        var releaseNotesRequest = new GitHubReleaseNotesRequest {
            TagName = tag,
            TargetCommitish = targetCommitish,
        };

        var generatedNotes = await _client.GenerateReleaseNotesAsync(
            RepositoryOwner,
            RepositoryName,
            releaseNotesRequest).ConfigureAwait(false);
        var body = $"We also have a [human-curated changelog]({GetFileUrl("CHANGELOG.md", _git.CurrentBranch)}).\n\n---\n\n"
                + generatedNotes.Body;

        _reporter.Info($"Publishing the previously created release as {tag} (target {targetCommitish})...");
        var update = new GitHubReleaseUpdate {
            TagName = tag,
            Name = tag,
            TargetCommitish = targetCommitish,
            Body = body,
            Prerelease = _version.IsPrerelease,
            Draft = false,
        };

        await _client.UpdateReleaseAsync(RepositoryOwner, RepositoryName, release.Id, update).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously deletes a release and, optionally, the corresponding tag on the GitHub repository.
    /// </summary>
    /// <param name="release">An object representing the release.</param>
    /// <param name="tagName">The tag name, or <see langword="null"/> to not delete a tag.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    /// <remarks>
    /// <para>A deleted release is named after <paramref name="tagName"/>, never after the tag name of
    /// <paramref name="release"/>: the draft is created before the release commit exists, and that commit
    /// always moves the version, so the tag name the draft carries is never the one the release is published
    /// under. A draft, having no tag of its own, is reported as the draft it is.</para>
    /// </remarks>
    public async Task DeleteReleaseAsync(GitHubRelease release, string? tagName)
    {
        Guard.IsNotNull(release);
        _reporter.Info("Deleting the previously created release...");
        await _client.DeleteReleaseAsync(RepositoryOwner, RepositoryName, release.Id).ConfigureAwait(false);
        if (string.IsNullOrEmpty(tagName))
        {
            _reporter.Notice("Deleted the provisional draft release.");
            return;
        }

        _reporter.Notice($"Deleted release {tagName}.");

        _reporter.Info($"Looking for tag {tagName} in GitHub repository...");
        if (!await _client.TagExistsAsync(RepositoryOwner, RepositoryName, tagName).ConfigureAwait(false))
        {
            _reporter.Info($"Tag {tagName} not found in GitHub repository.");
            return;
        }

        _reporter.Info($"Deleting tag {tagName} in GitHub repository...");
        await _client.DeleteTagAsync(RepositoryOwner, RepositoryName, tagName).ConfigureAwait(false);
        _reporter.Notice($"Deleted tag {tagName}.");
    }

    /// <summary>
    /// Asynchronously uploads a release asset.
    /// </summary>
    /// <param name="release">An object representing the release.</param>
    /// <param name="path">The full path of the asset file.</param>
    /// <param name="mimeType">The MIME type of the asset.</param>
    /// <param name="description">A short textual description of the asset, which becomes its label.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task UploadReleaseAssetAsync(GitHubRelease release, string path, string mimeType, string description)
    {
        _reporter.Detail($"Uploading asset {path}...");
        var assetContents = UserFile.OpenRead(path);
        await using (assetContents.ConfigureAwait(false))
        {
            await _client.UploadReleaseAssetAsync(
                release,
                Path.GetFileName(path),
                mimeType,
                string.IsNullOrEmpty(description) ? null : description,
                assetContents).ConfigureAwait(false);
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _client.Dispose();
}
