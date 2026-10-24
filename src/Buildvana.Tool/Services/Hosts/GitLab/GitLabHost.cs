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
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// The GitLab host: <c>gitlab.com</c>, or a self-managed GitLab instance.
/// </summary>
/// <remarks>
/// <para>The token is the job token of GitLab CI when <see cref="TokenEnv"/> is <c>CI_JOB_TOKEN</c>, and an
/// access token otherwise. GitLab exposes a job token through that variable alone, so its name decides.</para>
/// </remarks>
internal sealed class GitLabHost : GitHost, IDisposable
{
    /// <summary>
    /// The name of the variable that holds the job token of GitLab CI.
    /// </summary>
    public const string JobTokenVariable = "CI_JOB_TOKEN";

    private readonly IServiceProvider _services;
    private readonly GitLabConfig _config;
    private readonly GitLabRepositoryUrls _urls;
    private readonly HttpMessageHandler? _handler;

    private GitLabApiClient? _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLabHost"/> class.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="url">The parsed URL of the repository.</param>
    /// <param name="handler">The handler that sends the requests of the API client, or <see langword="null"/>
    /// for one that reaches the network. It is disposed together with this instance once the client exists.</param>
    public GitLabHost(IServiceProvider services, GitUrlInfo url, HttpMessageHandler? handler = null)
        : base(url)
    {
        Guard.IsNotNull(services);
        _services = services;
        _config = services.GetRequiredService<BuildvanaConfig>().Hosts.GitLab;
        ProjectPath = $"{Owner}/{Repository}";
        _urls = new GitLabRepositoryUrls(HostName, ProjectPath);
        _handler = handler;
    }

    /// <inheritdoc/>
    public override string Name => "GitLab";

    /// <inheritdoc/>
    /// <value>Always <see cref="HostKind.GitLab"/>.</value>
    public override HostKind? Kind => HostKind.GitLab;

    /// <inheritdoc/>
    public override Uri RepositoryUrl => _urls.Repository;

    /// <inheritdoc/>
    public override string TokenEnv => _config.TokenEnv;

    /// <inheritdoc/>
    /// <value><c>gitlab-ci-token</c> for a job token, <c>oauth2</c> for an access token.</value>
    public override string PushUsername => TokenKind == GitLabTokenKind.JobToken ? "gitlab-ci-token" : "oauth2";

    /// <summary>
    /// Gets the path of the project, such as <c>group/subgroup/project</c>.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Gets the kind of the token <see cref="TokenEnv"/> names.
    /// </summary>
    public GitLabTokenKind TokenKind
        => string.Equals(TokenEnv, JobTokenVariable, StringComparison.Ordinal) ? GitLabTokenKind.JobToken : GitLabTokenKind.AccessToken;

    /// <summary>
    /// Gets the client of the REST API of the host, created on first use.
    /// </summary>
    /// <exception cref="BuildFailedException">The variable <see cref="TokenEnv"/> names is not set.</exception>
    /// <remarks>
    /// <para>The client is created on first use, so that a host whose token is missing can still answer what
    /// needs no API call, such as the URLs and the push username.</para>
    /// </remarks>
    internal GitLabApiClient Client => _client ??= new GitLabApiClient(
        HostName,
        RuntimeAccess.Translate(_config.GetToken),
        TokenKind,
        _handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

    /// <inheritdoc/>
    public override Uri GetReleaseUrl(string tag) => _urls.Release(tag);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>A GitLab file URL takes the name of a tag, a branch, or a commit alike, so the kind of
    /// <paramref name="gitRef"/> plays no part.</para>
    /// </remarks>
    public override Uri GetFileUrl(string path, GitRef gitRef)
    {
        Guard.IsNotNull(gitRef);
        return _urls.File(path, gitRef.Name);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>A project of <c>internal</c> visibility counts as private, because only the users of the instance
    /// can see it.</para>
    /// <para>A job token cannot read a project, so with one the project is read without credentials. GitLab
    /// answers that request only for a public project.</para>
    /// </remarks>
    public override async Task<bool> IsPrivateRepositoryAsync()
    {
        _services.GetRequiredService<IReporter>().Info("Fetching project information...");
        var project = TokenKind == GitLabTokenKind.JobToken
            ? await Client.GetProjectWithoutCredentialsAsync(ProjectPath).ConfigureAwait(false)
            : await Client.GetProjectAsync(ProjectPath).ConfigureAwait(false);
        return project is not { Visibility: "public" };
    }

    /// <inheritdoc/>
    public override ReleasePublisher CreateReleasePublisher()
        => new GitLabReleasePublisher(
            this,
            _services.GetRequiredService<IReporter>(),
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
