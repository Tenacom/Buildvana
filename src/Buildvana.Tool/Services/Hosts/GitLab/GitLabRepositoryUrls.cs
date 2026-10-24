// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// Builds the URLs of a GitLab project and of the resources it contains.
/// </summary>
/// <remarks>
/// <para>The URL of a resource puts a <c>-</c> segment between the path of the project and the kind of the
/// resource, because a project path can hold any number of group segments.</para>
/// <para>The project URL carries no trailing slash, as <see cref="GitHub.GitHubRepositoryUrls"/> says of
/// a GitHub repository URL.</para>
/// </remarks>
internal sealed class GitLabRepositoryUrls
{
    private readonly string _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLabRepositoryUrls"/> class.
    /// </summary>
    /// <param name="hostName">The host name of the GitLab instance, such as <c>gitlab.com</c>.</param>
    /// <param name="projectPath">The path of the project, such as <c>group/subgroup/project</c>.</param>
    public GitLabRepositoryUrls(string hostName, string projectPath)
    {
        Guard.IsNotNullOrEmpty(hostName);
        Guard.IsNotNullOrEmpty(projectPath);

        _repository = $"https://{hostName}/{projectPath}";
        Repository = new Uri(_repository);
    }

    /// <summary>
    /// Gets the URL of the project, without a trailing slash.
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
        return new Uri($"{_repository}/-/releases/{tag}");
    }

    /// <summary>
    /// Builds the URL of a file of the project, as of a tag, a branch, or a commit.
    /// </summary>
    /// <param name="path">The path of the file, relative to the root of the repository.</param>
    /// <param name="commitish">The name of the tag or the branch, or the SHA of the commit.</param>
    /// <returns>The URL of the file.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is rooted, or leaves the repository, as
    /// <see cref="RepositoryFilePath.ToUrlPath"/> says.</exception>
    public Uri File(string path, string commitish)
    {
        Guard.IsNotNullOrEmpty(commitish);
        return new Uri($"{_repository}/-/blob/{commitish}/{RepositoryFilePath.ToUrlPath(path)}");
    }
}
