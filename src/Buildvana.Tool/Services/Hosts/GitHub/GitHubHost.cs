// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.Hosts.GitHub;

/// <summary>
/// The GitHub host: <c>github.com</c>, GitHub Enterprise Cloud with data residency, or a GitHub Enterprise Server.
/// </summary>
internal sealed class GitHubHost : GitHost, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly GitHubConfig _config;
    private readonly GitHubRepositoryUrls _urls;
    private readonly HttpMessageHandler? _handler;

    private GitHubApiClient? _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubHost"/> class.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="url">The parsed URL of the repository.</param>
    /// <param name="handler">The handler that sends the requests of the API client, or <see langword="null"/>
    /// for one that reaches the network. It is disposed together with this instance once the client exists.</param>
    public GitHubHost(IServiceProvider services, GitUrlInfo url, HttpMessageHandler? handler = null)
        : base(url)
    {
        Guard.IsNotNull(services);
        BuildFailedException.ThrowIfNot(url.PathSegments.Count == 2, $"'{url.Url}' is not a valid GitHub repository URL.");
        _services = services;
        _config = services.GetRequiredService<BuildvanaConfig>().Hosts.GitHub;
        _urls = new GitHubRepositoryUrls(HostName, Owner, Repository);
        _handler = handler;
    }

    /// <inheritdoc/>
    public override string Name => "GitHub";

    /// <inheritdoc/>
    /// <value>Always <see cref="HostKind.GitHub"/>.</value>
    public override HostKind? Kind => HostKind.GitHub;

    /// <inheritdoc/>
    public override Uri RepositoryUrl => _urls.Repository;

    /// <inheritdoc/>
    public override string TokenEnv => _config.TokenEnv;

    /// <inheritdoc/>
    /// <value>Always <c>x-access-token</c>.</value>
    public override string PushUsername => "x-access-token";

    /// <summary>
    /// Gets the client of the REST API of the host, created on first use.
    /// </summary>
    /// <exception cref="BuildFailedException">The variable <see cref="TokenEnv"/> names is not set.</exception>
    /// <remarks>
    /// <para>The client is created on first use, so that a host whose token is missing can still answer what
    /// needs no API call, such as the URLs and the push username.</para>
    /// </remarks>
    internal GitHubApiClient Client => _client ??= new GitHubApiClient(
        HostName,
        RuntimeAccess.Translate(_config.GetToken),
        _handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

    /// <inheritdoc/>
    public override Uri GetReleaseUrl(string tag) => _urls.ReleaseTag(tag);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>A GitHub file URL takes the name of a tag, a branch, or a commit alike, so the kind of
    /// <paramref name="gitRef"/> plays no part.</para>
    /// </remarks>
    public override Uri GetFileUrl(string path, GitRef gitRef)
    {
        Guard.IsNotNull(gitRef);
        return _urls.File(path, gitRef.Name);
    }

    /// <inheritdoc/>
    public override async Task<bool> IsPrivateRepositoryAsync()
    {
        _services.GetRequiredService<IReporter>().Info("Fetching repository information...");
        var repository = await Client.GetRepositoryAsync(Owner, Repository).ConfigureAwait(false);
        return repository.Private;
    }

    /// <inheritdoc/>
    public override ReleasePublisher CreateReleasePublisher()
        => new GitHubReleasePublisher(
            this,
            _services.GetRequiredService<IReporter>(),
            _services.GetRequiredService<VersionService>(),
            _services.GetRequiredService<IGitService>());

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        if (_client is not null)
        {
            _client.Dispose();
        }
        else
        {
            _handler?.Dispose();
        }
    }
}
