// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Services.Git;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitLab;

/// <summary>
/// Continuous Integration adapter for GitLab.
/// </summary>
internal sealed class GitLabServerAdapter : ServerAdapter
{
    internal GitLabServerAdapter()
    {
        CIBotIdentity = new("GitLab CI", $"gitlab-ci@noreply.{Environment.GetEnvironmentVariable("CI_SERVER_HOST")}");
    }

    /// <inheritdoc/>
    public override string Name => "GitLab CI";

    /// <inheritdoc/>
    public override string HostName => BuildFailedException.ThrowOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    public override string RepositoryOwner => BuildFailedException.ThrowOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    public override string RepositoryName => BuildFailedException.ThrowOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    public override Uri RepositoryUrl => BuildFailedException.ThrowOnUnsupportedProperty<Uri>();

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool IsCloudBuild => true;

    /// <inheritdoc/>
    public override GitIdentity? CIBotIdentity { get; }

    /// <inheritdoc/>
    public override string PushUsername => "oauth2";

    /// <inheritdoc/>
    public override string? PushPassword => null;

    /// <summary>
    /// Creates and returns an instance of <see cref="GitLabServerAdapter"/> if the build is running in a GitLab CI runner.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>If the build is running in GitLab CI, a newly-created <see cref="GitLabServerAdapter"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static ServerAdapter? CreateIfApplicable(IServiceProvider services)
    {
        Guard.IsNotNull(services);

        return Environment.GetEnvironmentVariable("GITLAB_CI") is not null
            ? new GitLabServerAdapter()
            : null;
    }

    /// <inheritdoc/>
    public override Task<bool> IsPrivateRepositoryAsync() => BuildFailedException.ThrowOnUnsupportedMethod<Task<bool>>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetReleaseUrl(string version)
        => BuildFailedException.ThrowOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetFileUrl(string path, string commitish)
        => BuildFailedException.ThrowOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    public override Task<ServerRelease> CreateReleaseAsync() => BuildFailedException.ThrowOnUnsupportedMethod<Task<ServerRelease>>();
}
