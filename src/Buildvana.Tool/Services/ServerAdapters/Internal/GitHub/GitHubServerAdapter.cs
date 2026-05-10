// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Configuration;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// Continuous Integration adapter for GitHub.
/// </summary>
internal sealed class GitHubServerAdapter : ServerAdapter
{
    private readonly IServiceProvider _services;
    private readonly ILogger<GitHubServerAdapter> _logger;
    private readonly VersionService _version;
    private readonly GitService _git;

    private readonly string _token;

    private GitHubServerAdapter(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<GitHubServerAdapter>>();
        _version = services.GetRequiredService<VersionService>();
        _git = services.GetRequiredService<GitService>();
        BuildFailedException.ThrowIfNot(GitUrlInfo.TryCreate(_git.OriginUrl, out var originInfo), $"Couldn't get information from origin URL '{_git.OriginUrl}'.");
        BuildFailedException.ThrowIfNot(originInfo.PathSegments.Count == 2, $"'{originInfo.Url}' is not a valid GitHub repository URL.");
        HostName = originInfo.Host;
        RepositoryOwner = originInfo.PathSegments[0];
        RepositoryName = originInfo.PathSegments[1];
        RepositoryUrl = new Uri($"https://{HostName}/{RepositoryOwner}/{RepositoryName}");
        _token = services.GetRequiredService<ToolConfiguration>().GitHubToken;
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
    public override Uri RepositoryUrl { get; }

    /// <inheritdoc/>
    /// <value>Always <see langword="false"/>.</value>
    public override bool IsCloudBuild => true;

    /// <inheritdoc/>
    public override GitIdentity? CIBotIdentity { get; } = new("github-actions[bot]", "41898282+github-actions[bot]@users.noreply.github.com");

    /// <inheritdoc/>
    public override string PushUsername => "x-access-token";

    /// <inheritdoc/>
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

    /// <summary>
    /// Sets a GitHub Actions step output.
    /// </summary>
    /// <param name="name">The output name.</param>
    /// <param name="value">The output value.</param>
    public static void SetActionsStepOutput(string name, string value)
    {
        var outputFile = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        BuildFailedException.ThrowIfNot(!string.IsNullOrEmpty(outputFile), "Cannot set Actions step output: GITHUB_OUTPUT not set.");
        File.AppendAllLines(outputFile, [$"{name}={value}"], Encoding.UTF8);
    }

    /// <inheritdoc/>
    public override async Task<bool> IsPrivateRepositoryAsync()
    {
        _logger.LogInformation("Fetching repository information...");
        var client = CreateGitHubClient();
        var repository = await client.Repository.Get(RepositoryOwner, RepositoryName).ConfigureAwait(false);
        return repository.Private;
    }

    /// <inheritdoc/>
    public override Uri GetReleaseUrl(string version)
    {
        Guard.IsNotNullOrEmpty(version);
        return new Uri($"{RepositoryUrl}releases/tag/{version}");
    }

    /// <inheritdoc/>
    public override Uri GetFileUrl(string path, string commitish)
    {
        Guard.IsNotNullOrEmpty(path);
        Guard.IsNotNullOrEmpty(commitish);
        Guard.IsTrue(!Path.IsPathFullyQualified(path), "A path must be relative to be converted to a file URL.");

        // Normalize to forward slashes for the URL, then reject paths that escape the repo.
        var remotePath = path.Replace('\\', '/');
        Guard.IsTrue(remotePath != ".." && !remotePath.StartsWith("../", StringComparison.Ordinal), "Only a path to a file in the repository can be converted to a file URL.");

        return new Uri($"{RepositoryUrl}blob/{commitish}/{remotePath}");
    }

    /// <inheritdoc/>
    public override async Task<ServerRelease> CreateReleaseAsync()
        => await GitHubServerRelease.CreateAsync(
            server: this,
            services: _services,
            createGitHubReleaseAsync: () =>
            {
                // Create the release as a draft first, so if the token has no permissions we can bail out early
                var tag = _version.CurrentStr;
                var client = CreateGitHubClient();
                _logger.LogInformation("Creating a provisional draft release...");
                var newRelease = new NewRelease(tag)
                {
                    Name = $"{tag} [provisional]",
                    TargetCommitish = _git.CurrentBranch,
                    Prerelease = _version.IsPrerelease,
                    Draft = true,
                };

                return client.Repository.Release.Create(RepositoryOwner, RepositoryName, newRelease);
            }).ConfigureAwait(false);

    /// <summary>
    /// Asynchronously publishes a draft release on the GitHub repository.
    /// </summary>
    /// <param name="release">An object representing the GitHub release.</param>
    /// <param name="targetCommitish">The commit the release tag should point to. Pass the SHA of the
    /// release commit so the tag is anchored to it regardless of any post-release commits that may
    /// have been pushed on top of the branch.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task PublishReleaseAsync(Release release, string targetCommitish)
    {
        Guard.IsNotNull(release);
        Guard.IsNotNullOrEmpty(targetCommitish);
        var tag = _version.CurrentStr;
        var client = CreateGitHubClient();
        _logger.LogInformation("Generating release notes for {Tag}...", tag);
        var releaseNotesRequest = new GenerateReleaseNotesRequest(tag)
        {
            TargetCommitish = targetCommitish,
        };

        var generateNotesResponse = await client.Repository.Release.GenerateReleaseNotes(RepositoryOwner, RepositoryName, releaseNotesRequest).ConfigureAwait(false);
        var body = $"We also have a [human-curated changelog]({GetFileUrl("CHANGELOG.md", _git.MainBranch)}).\n\n---\n\n"
                + generateNotesResponse.Body;

        _logger.LogInformation("Publishing the previously created release as {Tag} (target {TargetCommitish})...", tag, targetCommitish);
        var update = release.ToUpdate();
        update.TagName = tag;
        update.Name = tag;
        update.TargetCommitish = targetCommitish;
        update.Body = body;
        update.Prerelease = _version.IsPrerelease;
        update.Draft = false;

        _ = await client.Repository.Release.Edit(RepositoryOwner, RepositoryName, release.Id, update).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously deletes a release and, optionally, the corresponding tag on the GitHub repository.
    /// </summary>
    /// <param name="release">An object representing the release.</param>
    /// <param name="tagName">The tag name, or <see langword="null"/> to not delete a tag.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task DeleteReleaseAsync(Release release, string? tagName)
    {
        Guard.IsNotNull(release);
        _logger.LogInformation("Deleting the previously created release...");
        var client = CreateGitHubClient();
        await client.Repository.Release.Delete(RepositoryOwner, RepositoryName, release.Id).ConfigureAwait(false);
        if (string.IsNullOrEmpty(tagName))
        {
            return;
        }

        var reference = "refs/tags/" + tagName;
        _logger.LogInformation("Looking for reference '{Reference}' in GitHub repository...", reference);
        try
        {
            _ = await client.Git.Reference.Get(RepositoryOwner, RepositoryName, reference).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            _logger.LogInformation("Reference '{Reference}' not found in GitHub repository.", reference);
            return;
        }

        _logger.LogInformation("Deleting reference '{Reference}' in GitHub repository...", reference);
        await client.Git.Reference.Delete(RepositoryOwner, RepositoryName, reference).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously uploads a release asset.
    /// </summary>
    /// <param name="release">An object representing the release.</param>
    /// <param name="path">The full path of the asset file.</param>
    /// <param name="mimeType">The MIME type of the asset.</param>
    /// <param name="description">A short textual description of the asset.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task UploadReleaseAssetAsync(Release release, string path, string mimeType, string description)
    {
        var client = CreateGitHubClient();
        _logger.LogDebug("Uploading asset {Path}...", path);
        ReleaseAsset asset;
        var assetContents = File.OpenRead(path);
        await using (assetContents.ConfigureAwait(false))
        {
            var upload = new ReleaseAssetUpload()
            {
                FileName = Path.GetFileName(path),
                ContentType = mimeType,
                RawData = assetContents,
            };

            asset = await client.Repository.Release.UploadAsset(release, upload).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(description))
        {
            _logger.LogDebug("Updating asset label...");
            var update = asset.ToUpdate();
            update.Label = description;
            _ = await client.Repository.Release.EditAsset(RepositoryOwner, RepositoryName, asset.Id, update).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Skipping label update: asset has no description.");
        }
    }

    private GitHubClient CreateGitHubClient()
    {
        var client = new GitHubClient(new ProductHeaderValue("Buildvana"))
        {
            Credentials = new(_token),
        };

        return client;
    }
}
