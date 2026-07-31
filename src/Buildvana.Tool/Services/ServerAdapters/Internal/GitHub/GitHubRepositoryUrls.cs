// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;

/// <summary>
/// Builds the URLs of a GitHub repository and of the resources it contains.
/// </summary>
/// <remarks>
/// <para>This type owns both ends of the same contract: how the repository URL is built, and how the URLs of
/// resources are derived from it. Keeping them together is the point — deriving a resource URL elsewhere,
/// by appending to the repository URL, is what previously produced links with no separator between the
/// repository name and the first path segment.</para>
/// <para>The repository URL deliberately carries no trailing slash, so it can be displayed and compared as
/// the canonical URL of the repository. Callers must therefore not treat it as a base URL for
/// <see cref="Uri(Uri,string)"/>, which would resolve a relative URL against its parent and drop the
/// repository name.</para>
/// </remarks>
internal sealed class GitHubRepositoryUrls
{
    private readonly string _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubRepositoryUrls"/> class.
    /// </summary>
    /// <param name="hostName">The host name of the GitHub instance, e.g. <c>github.com</c>.</param>
    /// <param name="repositoryOwner">The owner of the repository.</param>
    /// <param name="repositoryName">The name of the repository.</param>
    public GitHubRepositoryUrls(string hostName, string repositoryOwner, string repositoryName)
    {
        Guard.IsNotNullOrEmpty(hostName);
        Guard.IsNotNullOrEmpty(repositoryOwner);
        Guard.IsNotNullOrEmpty(repositoryName);

        _repository = $"https://{hostName}/{repositoryOwner}/{repositoryName}";
        Repository = new Uri(_repository);
    }

    /// <summary>
    /// Gets the URL of the repository, without a trailing slash.
    /// </summary>
    public Uri Repository { get; }

    /// <summary>
    /// Builds the URL of the release identified by a tag.
    /// </summary>
    /// <param name="version">The version string, which is also the tag name.</param>
    /// <returns>The URL of the release.</returns>
    public Uri ReleaseTag(string version)
    {
        Guard.IsNotNullOrEmpty(version);
        return new Uri($"{_repository}/releases/tag/{version}");
    }

    /// <summary>
    /// Builds the URL of a file in the repository, as of a given commit or reference.
    /// </summary>
    /// <param name="path">The path to the file, relative to the repository root.</param>
    /// <param name="commitish">The SHA or reference to which the file belongs.</param>
    /// <returns>The URL of the file.</returns>
    public Uri File(string path, string commitish)
    {
        Guard.IsNotNullOrEmpty(path);
        Guard.IsNotNullOrEmpty(commitish);
        Guard.IsTrue(!Path.IsPathFullyQualified(path), nameof(path), "A path must be relative to be converted to a file URL.");

        // Normalize to forward slashes for the URL, then reject paths that escape the repo.
        // Every ".." segment must go, not just a leading one: Uri collapses parent segments as it parses,
        // so enough of them anywhere in the path walk out of the repository and even out of the owner
        // (".../blob/main/docs/../../../../../../etc/passwd" parses to "https://github.com/etc/passwd").
        var remotePath = path.Replace('\\', '/');
        var hasParentSegment = remotePath == ".."
            || remotePath.StartsWith("../", StringComparison.Ordinal)
            || remotePath.EndsWith("/..", StringComparison.Ordinal)
            || remotePath.Contains("/../", StringComparison.Ordinal);
        Guard.IsTrue(!hasParentSegment, nameof(path), "Only a path to a file in the repository can be converted to a file URL.");

        return new Uri($"{_repository}/blob/{commitish}/{remotePath}");
    }
}
