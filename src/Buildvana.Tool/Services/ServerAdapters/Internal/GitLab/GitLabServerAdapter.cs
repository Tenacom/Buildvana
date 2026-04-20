// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Buildvana.Tool.Utilities;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters.Internal.GitLab;

/// <summary>
/// Continuous Integration adapter for GitLab.
/// </summary>
internal sealed class GitLabServerAdapter : ServerAdapter
{
    private readonly ICakeContext _context;

    internal GitLabServerAdapter(IServiceProvider services)
    {
        _context = services.GetRequiredService<ICakeContext>();
    }

    /// <inheritdoc/>
    public override string Name => "GitLab CI";

    /// <inheritdoc/>
    public override string HostName => _context.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    public override string RepositoryOwner => _context.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    public override string RepositoryName => _context.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    public override Uri RepositoryUrl => _context.FailOnUnsupportedProperty<Uri>();

    /// <inheritdoc/>
    /// <value>Always <see langword="true"/>.</value>
    public override bool IsCloudBuild => true;

    /// <summary>
    /// Creates and returns an instance of <see cref="GitLabServerAdapter"/> if the build is running in a GitLab CI runner.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>If the build is running in GitLab CI, a newly-created <see cref="GitLabServerAdapter"/>;
    /// otherwise, <see langword="null"/>.</returns>
    public static ServerAdapter? CreateIfApplicable(IServiceProvider services)
    {
        Guard.IsNotNull(services);

        var context = services.GetRequiredService<ICakeContext>();

        return context.HasEnvironmentVariable("GITLAB_CI")
            ? new GitLabServerAdapter(services)
            : null;
    }

    /// <inheritdoc/>
    public override Task<bool> IsPrivateRepositoryAsync() => _context.FailOnUnsupportedMethod<Task<bool>>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetReleaseUrl(string version)
        => _context.FailOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetFileUrl(FilePath path, string commitish)
        => _context.FailOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    public override Task<ServerRelease> CreateReleaseAsync() => _context.FailOnUnsupportedMethod<Task<ServerRelease>>();
}
