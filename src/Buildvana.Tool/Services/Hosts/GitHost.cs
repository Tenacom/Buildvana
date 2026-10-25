// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts.Gitea;
using Buildvana.Tool.Services.Hosts.GitHub;
using Buildvana.Tool.Services.Hosts.GitLab;
using Buildvana.Tool.Services.Platforms;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Base class for the server that owns the repository the releases go to, as <c>repository.url</c> names it.
/// </summary>
internal abstract class GitHost
{
    private protected GitHost(GitUrlInfo url)
    {
        Guard.IsNotNull(url);
        BuildFailedException.ThrowIf(
            url.PathSegments.Count < 2,
            $"'{url.Url}' names no repository: the path of a repository URL holds an owner and a name.");
        HostName = url.Host;
        Owner = string.Join('/', url.PathSegments.Take(url.PathSegments.Count - 1));
        Repository = url.PathSegments[^1];
    }

    /// <summary>
    /// Gets the display name of the host.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the kind of the host, or <see langword="null"/> for a host <c>bv</c> does not know.
    /// </summary>
    public abstract HostKind? Kind { get; }

    /// <summary>
    /// Gets the host name of the server.
    /// </summary>
    public string HostName { get; }

    /// <summary>
    /// Gets the owner of the repository: every segment of the path of the repository URL but the last.
    /// </summary>
    public string Owner { get; }

    /// <summary>
    /// Gets the name of the repository: the last segment of the path of the repository URL.
    /// </summary>
    public string Repository { get; }

    /// <summary>
    /// Gets the URL of the web page of the repository, or <see langword="null"/> for a host with no URL scheme.
    /// </summary>
    public abstract Uri? RepositoryUrl { get; }

    /// <summary>
    /// Gets the name of the environment variable holding the API token of the host, or <see langword="null"/>
    /// for a host that has none.
    /// </summary>
    /// <remarks>
    /// <para>The token is also the password <c>bv</c> pushes with.</para>
    /// </remarks>
    public abstract string? TokenEnv { get; }

    /// <summary>
    /// Gets the username a push presents along with the token, or <see langword="null"/> for a host with no
    /// such convention.
    /// </summary>
    public abstract string? PushUsername { get; }

    /// <summary>
    /// Creates the host of the repository that <c>repository.url</c> names.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>A newly-created <see cref="GitHost"/>.</returns>
    /// <exception cref="BuildFailedException">The configuration states no <c>repository.url</c>, or one
    /// that is not a Git URL.</exception>
    public static GitHost Create(IServiceProvider services)
    {
        Guard.IsNotNull(services);
        var config = services.GetRequiredService<BuildvanaConfig>();
        var url = ParseRepositoryUrl(config.Repository);
        return DetectKind(config.Repository, url, services.GetRequiredService<CIPlatform>()) switch {
            HostKind.GitHub => new GitHubHost(services, url),
            HostKind.GitLab => new GitLabHost(services, url),
            HostKind.Gitea => new GiteaHost(services, url),
            _ => new UnknownHost(url, services.GetRequiredService<IReporter>()),
        };
    }

    /// <summary>
    /// Parses the URL of the repository the releases go to.
    /// </summary>
    /// <param name="repository">The repository configuration.</param>
    /// <returns>The parsed URL.</returns>
    /// <exception cref="BuildFailedException"><see cref="RepositoryConfig.Url"/> is <see langword="null"/>,
    /// or not a Git URL.</exception>
    public static GitUrlInfo ParseRepositoryUrl(RepositoryConfig repository)
    {
        Guard.IsNotNull(repository);
        var url = repository.Url;
        BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(url),
            "The configuration file states no repository.url: set it to the URL of the repository the releases go to.");
        BuildFailedException.ThrowIfNot(GitUrlInfo.TryCreate(url, out var result), $"repository.url '{url}' is not a Git URL.");
        return result;
    }

    /// <summary>
    /// Detects the kind of a host.
    /// </summary>
    /// <param name="repository">The repository configuration.</param>
    /// <param name="url">The parsed URL of the repository.</param>
    /// <param name="platform">The CI platform <c>bv</c> runs on.</param>
    /// <returns>The first of these that applies: <see cref="RepositoryConfig.Host"/> when it is not
    /// <see cref="HostKind.Auto"/>; the kind of a host that GitHub, GitLab, or Gitea runs, such as
    /// <c>github.com</c>, when <paramref name="url"/> names it; the
    /// <see cref="CIPlatform.ServerKind"/> of a platform whose <see cref="CIPlatform.ServerUrl"/> has the host
    /// name of <paramref name="url"/>; otherwise, <see langword="null"/>.</returns>
    public static HostKind? DetectKind(RepositoryConfig repository, GitUrlInfo url, CIPlatform platform)
    {
        Guard.IsNotNull(repository);
        Guard.IsNotNull(url);
        Guard.IsNotNull(platform);
        if (repository.Host != HostKind.Auto)
        {
            return repository.Host;
        }

        // A host whose name ends in .ghe.com belongs to GitHub Enterprise Cloud with data residency.
        var isGitHub = string.Equals(url.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || url.Host.EndsWith(".ghe.com", StringComparison.OrdinalIgnoreCase);
        if (isGitHub)
        {
            return HostKind.GitHub;
        }

        if (string.Equals(url.Host, "gitlab.com", StringComparison.OrdinalIgnoreCase))
        {
            return HostKind.GitLab;
        }

        if (string.Equals(url.Host, "gitea.com", StringComparison.OrdinalIgnoreCase))
        {
            return HostKind.Gitea;
        }

        // A self-hosted server, such as a GitHub Enterprise Server or a self-managed GitLab instance, is recognized
        // on its own runners.
        var isPlatformServer = string.Equals(platform.ServerUrl?.Host, url.Host, StringComparison.OrdinalIgnoreCase);
        return isPlatformServer ? platform.ServerKind : null;
    }

    /// <summary>
    /// Gets the API token of the host: the value of the environment variable <see cref="TokenEnv"/> names,
    /// read at the moment of the call.
    /// </summary>
    /// <returns>The token, or <see langword="null"/> if the host has no token variable, or the variable is
    /// not set or empty.</returns>
    public string? GetToken()
        => TokenEnv is { } name && Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    /// <summary>
    /// Gets the URL of the release a tag names.
    /// </summary>
    /// <param name="tag">The name of the release tag.</param>
    /// <returns>The URL of the release, or <see langword="null"/> for a host with no URL scheme.</returns>
    public abstract Uri? GetReleaseUrl(string tag);

    /// <summary>
    /// Gets the URL of a file of the repository, as of a reference.
    /// </summary>
    /// <param name="path">The path of the file, relative to the root of the repository.</param>
    /// <param name="gitRef">The tag, branch, or commit the file belongs to.</param>
    /// <returns>The URL of the file, or <see langword="null"/> for a host with no URL scheme.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is rooted, or leaves the repository.</exception>
    public abstract Uri? GetFileUrl(string path, GitRef gitRef);

    /// <summary>
    /// Asynchronously asks the host whether the repository is private.
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// <see langword="true"/> for a private repository and <see langword="false"/> for a public one.</returns>
    public abstract Task<bool> IsPrivateRepositoryAsync();

    /// <summary>
    /// Creates the host side of one release.
    /// </summary>
    /// <returns>A newly-created <see cref="ReleasePublisher"/>.</returns>
    public abstract ReleasePublisher CreateReleasePublisher();
}
