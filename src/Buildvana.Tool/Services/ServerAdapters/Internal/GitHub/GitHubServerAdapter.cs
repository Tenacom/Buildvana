// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Threading.Tasks;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;
using Cake.Http;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Octokit;

using SysFile = System.IO.File;
using SysPath = System.IO.Path;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// Continuous Integration adapter for GitHub.
/// </summary>
internal sealed class GitHubServerAdapter : ServerAdapter
{
    private readonly IServiceProvider _services;
    private readonly ICakeContext _context;
    private readonly VersionService _version;
    private readonly GitService _git;

    private readonly string _token;

    private GitHubServerAdapter(IServiceProvider services)
    {
        _services = services;
        _context = services.GetRequiredService<ICakeContext>();
        _version = services.GetRequiredService<VersionService>();
        _git = services.GetRequiredService<GitService>();
        _context.Ensure(GitUrlInfo.TryCreate(_git.OriginUrl, out var originInfo), $"Couldn't get information from origin URL '{_git.OriginUrl}'.");
        _context.Ensure(originInfo.PathSegments.Count == 2, $"'{originInfo.Url}' is not a valid GitHub repository URL.");
        HostName = originInfo.Host;
        RepositoryOwner = originInfo.PathSegments[0];
        RepositoryName = originInfo.PathSegments[1];
        RepositoryUrl = new Uri($"https://{HostName}/{RepositoryOwner}/{RepositoryName}");
        _token = services.GetRequiredService<OptionsService>().GetOptionOrFail<string>("githubToken");
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

    /// <summary>
    /// Creates and returns an instance of <see cref="GitHubServerAdapter"/> if the build is running in a GitHub Actions runner.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>If the build is running in a GitHub Actions runner, a newly-created <see cref="GitHubServerAdapter"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static ServerAdapter? CreateIfApplicable(IServiceProvider services)
    {
        Guard.IsNotNull(services);
        var context = services.GetRequiredService<ICakeContext>();
        return context.EnvironmentVariable("GITHUB_ACTIONS", false)
            ? new GitHubServerAdapter(services)
            : null;
    }

    /// <inheritdoc/>
    public override async Task<bool> IsPrivateRepositoryAsync()
    {
        _context.Information("Fetching repository information...");
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
    public override Uri GetFileUrl(FilePath path, string commitish)
    {
        Guard.IsNotNull(path);
        Guard.IsTrue(path.IsRelative, "A path must be relative to be converted to a file URL.");
        Guard.IsTrue(path.Segments[0] != "..", "Only a path to a file in the repository can be converted to a file URL.");
        Guard.IsNotNullOrEmpty(commitish);

        var remotePath = path.ToString();
        if (path.Separator != '/')
        {
            remotePath = remotePath.Replace(path.Separator, '/');
        }

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
                _context.Information("Creating a provisional draft release...");
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
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task PublishReleaseAsync(Release release)
    {
        Guard.IsNotNull(release);
        var tag = _version.CurrentStr;
        var client = CreateGitHubClient();
        _context.Information($"Generating release notes for {tag}...");
        var releaseNotesRequest = new GenerateReleaseNotesRequest(tag)
        {
            TargetCommitish = _git.CurrentBranch,
        };

        var generateNotesResponse = await client.Repository.Release.GenerateReleaseNotes(RepositoryOwner, RepositoryName, releaseNotesRequest).ConfigureAwait(false);
        var body = $"We also have a [human-curated changelog]({GetFileUrl("CHANGELOG.md", _git.MainBranch)}).\n\n---\n\n"
                + generateNotesResponse.Body;

        _context.Information($"Publishing the previously created release as {tag}...");
        var update = release.ToUpdate();
        update.TagName = tag;
        update.Name = tag;
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
        _context.Information("Deleting the previously created release...");
        var client = CreateGitHubClient();
        await client.Repository.Release.Delete(RepositoryOwner, RepositoryName, release.Id).ConfigureAwait(false);
        if (string.IsNullOrEmpty(tagName))
        {
            return;
        }

        var reference = "refs/tags/" + tagName;
        _context.Information($"Looking for reference '{reference}' in GitHub repository...");
        try
        {
            _ = await client.Git.Reference.Get(RepositoryOwner, RepositoryName, reference).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            _context.Information($"Reference '{reference}' not found in GitHub repository.");
            return;
        }

        _context.Information($"Deleting reference '{reference}' in GitHub repository...");
        await client.Git.Reference.Delete(RepositoryOwner, RepositoryName, reference).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously creates a workflow dispatch event on the GitHub repository.
    /// </summary>
    /// <param name="filename">The name of the workflow file to run, including extension.</param>
    /// <param name="reference">The name of the reference on which to dispatch the workflow run.</param>
    /// <param name="inputs">An optional anonymous object containing the inputs for the workflow. The object, if given, must be JSON-serializable.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task DispatchWorkflowAsync(string filename, string reference, object? inputs = null)
    {
        Guard.IsNotNullOrEmpty(filename);
        Guard.IsNotNullOrEmpty(reference);
        _context.Information($"Dispatching workflow '{filename}' on '{reference}'...");
        object requestBody = inputs is null
            ? new { @ref = reference }
            : new { @ref = reference, inputs };

        // TODO: Check whether this can be done with latest OctoKit instead of directly creating an HTTP request
        var httpSettings = new HttpSettings()
            .SetAccept("application/vnd.github.v3")
            .AppendHeader("Authorization", "Token " + _token)
            .AppendHeader("User-Agent", "Buildvana (Win32NT 10.0.19044; amd64; en-US)")
            .SetJsonRequestBody(requestBody)
            .EnsureSuccessStatusCode();

        var postUrl = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/actions/workflows/{filename}/dispatches";
        _ = await _context.HttpPostAsync(postUrl, httpSettings).ConfigureAwait(false);
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
        _context.Verbose($"Uploading asset {path}...");
        ReleaseAsset asset;
        var assetContents = SysFile.OpenRead(path);
        await using (assetContents.ConfigureAwait(false))
        {
            var upload = new ReleaseAssetUpload()
            {
                FileName = SysPath.GetFileName(path),
                ContentType = mimeType,
                RawData = assetContents,
            };

            asset = await client.Repository.Release.UploadAsset(release, upload).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(description))
        {
            _context.Verbose("Updating asset label...");
            var update = asset.ToUpdate();
            update.Label = description;
            _ = await client.Repository.Release.EditAsset(RepositoryOwner, RepositoryName, asset.Id, update).ConfigureAwait(false);
        }
        else
        {
            _context.Verbose("Skipping label update: asset has no description.");
        }
    }

    /// <summary>
    /// Sets a GitHub Actions step output.
    /// </summary>
    /// <param name="name">The output name.</param>
    /// <param name="value">The output value.</param>
    public void SetActionsStepOutput(string name, string value)
    {
        var outputFile = _context.EnvironmentVariable("GITHUB_OUTPUT");
        _context.Ensure(!string.IsNullOrEmpty(outputFile), "Cannot set Actions step output: GITHUB_OUTPUT not set.");
        SysFile.AppendAllLines(outputFile, [$"{name}={value}"], Encoding.UTF8);
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
