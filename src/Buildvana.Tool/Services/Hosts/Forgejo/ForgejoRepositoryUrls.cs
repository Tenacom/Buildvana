// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts.Forgejo;

/// <summary>
/// Builds the URLs of a Forgejo repository and of the resources it contains.
/// </summary>
/// <remarks>
/// <para>The repository URL carries no trailing slash, as <see cref="GitHub.GitHubRepositoryUrls"/> says of
/// a GitHub repository URL.</para>
/// </remarks>
internal sealed class ForgejoRepositoryUrls
{
    private readonly string _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForgejoRepositoryUrls"/> class.
    /// </summary>
    /// <param name="hostName">The host name of the Forgejo instance, such as <c>codeberg.org</c>.</param>
    /// <param name="repositoryOwner">The owner of the repository.</param>
    /// <param name="repositoryName">The name of the repository.</param>
    public ForgejoRepositoryUrls(string hostName, string repositoryOwner, string repositoryName)
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
    /// Builds the URL of the release of a tag.
    /// </summary>
    /// <param name="tag">The name of the tag.</param>
    /// <returns>The URL of the release.</returns>
    public Uri Release(string tag)
    {
        Guard.IsNotNullOrEmpty(tag);
        return new Uri($"{_repository}/releases/tag/{tag}");
    }

    /// <summary>
    /// Builds the URL of a file of the repository, as of a tag, a branch, or a commit.
    /// </summary>
    /// <param name="path">The path of the file, relative to the root of the repository.</param>
    /// <param name="gitRef">The tag, branch, or commit the file belongs to.</param>
    /// <returns>The URL of the file.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is rooted, or leaves the repository, as
    /// <see cref="RepositoryFilePath.ToUrlPath"/> says.</exception>
    /// <remarks>
    /// <para>A Forgejo file URL names the kind of the reference before its name, so that a tag and a branch of
    /// the same name lead to different files.</para>
    /// </remarks>
    public Uri File(string path, GitRef gitRef)
    {
        Guard.IsNotNull(gitRef);
        var kind = gitRef.Kind switch {
            GitRefKind.Tag => "tag",
            GitRefKind.Branch => "branch",
            GitRefKind.Commit => "commit",
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<string>(nameof(gitRef), gitRef.Kind, "Unknown kind of reference."),
        };

        return new Uri($"{_repository}/src/{kind}/{gitRef.Name}/{RepositoryFilePath.ToUrlPath(path)}");
    }
}
