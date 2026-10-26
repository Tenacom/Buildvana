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

namespace Buildvana.Tool.Services.Hosts.Forgejo;

/// <summary>
/// The Forgejo host: <c>codeberg.org</c>, or a self-hosted Forgejo instance.
/// </summary>
internal sealed class ForgejoHost : GitHost, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ForgejoConfig _config;
    private readonly ForgejoRepositoryUrls _urls;
    private readonly HttpMessageHandler? _handler;

    private ForgejoApiClient? _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForgejoHost"/> class.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="url">The parsed URL of the repository.</param>
    /// <param name="handler">The handler that sends the requests of the API client, or <see langword="null"/>
    /// for one that reaches the network. It is disposed together with this instance once the client exists.</param>
    public ForgejoHost(IServiceProvider services, GitUrlInfo url, HttpMessageHandler? handler = null)
        : base(url)
    {
        Guard.IsNotNull(services);
        BuildFailedException.ThrowIfNot(url.PathSegments.Count == 2, $"'{url.Url}' is not a valid Forgejo repository URL.");
        _services = services;
        _config = services.GetRequiredService<BuildvanaConfig>().Hosts.Forgejo;
        _urls = new ForgejoRepositoryUrls(HostName, Owner, Repository);
        _handler = handler;
    }

    /// <inheritdoc/>
    public override string Name => "Forgejo";

    /// <inheritdoc/>
    /// <value>Always <see cref="HostKind.Forgejo"/>.</value>
    public override HostKind? Kind => HostKind.Forgejo;

    /// <inheritdoc/>
    public override Uri RepositoryUrl => _urls.Repository;

    /// <inheritdoc/>
    public override string TokenEnv => _config.TokenEnv;

    /// <inheritdoc/>
    /// <value>Always <c>token</c>.</value>
    /// <remarks>
    /// <para>Forgejo ignores the username when the password is an access token or an Actions token.</para>
    /// </remarks>
    public override string PushUsername => "token";

    /// <summary>
    /// Gets the client of the REST API of the host, created on first use.
    /// </summary>
    /// <exception cref="BuildFailedException">The variable <see cref="TokenEnv"/> names is not set.</exception>
    /// <remarks>
    /// <para>The client is created on first use, so that a host whose token is missing can still answer what
    /// needs no API call, such as the URLs and the push username.</para>
    /// </remarks>
    internal ForgejoApiClient Client => _client ??= new ForgejoApiClient(
        HostName,
        RuntimeAccess.Translate(_config.GetToken),
        _handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

    /// <inheritdoc/>
    public override Uri GetReleaseUrl(string tag) => _urls.Release(tag);

    /// <inheritdoc/>
    public override Uri GetFileUrl(string path, GitRef gitRef) => _urls.File(path, gitRef);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>A repository whose owner has <c>limited</c> or <c>private</c> visibility counts as private, because
    /// Forgejo hides it from anonymous users even when the repository itself is not private.</para>
    /// </remarks>
    public override async Task<bool> IsPrivateRepositoryAsync()
    {
        _services.GetRequiredService<IReporter>().Info("Fetching repository information...");
        var repository = await Client.GetRepositoryAsync(Owner, Repository).ConfigureAwait(false);
        return repository.Private || repository.Owner.Visibility is not "public";
    }

    /// <inheritdoc/>
    public override ReleasePublisher CreateReleasePublisher()
        => new ForgejoReleasePublisher(
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
