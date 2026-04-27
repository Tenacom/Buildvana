// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.ServerAdapters.Internal;
using Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;
using Buildvana.Tool.Services.ServerAdapters.Internal.GitLab;
using Cake.Core.IO;

namespace Buildvana.Tool.Services.ServerAdapters;

/// <summary>
/// Base class for Continuous Integration adapters.
/// </summary>
public abstract class ServerAdapter
{
    private protected ServerAdapter()
    {
    }

    /// <summary>
    /// Gets the name of the build platform.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the name of the remote repository's host.
    /// </summary>
    public abstract string HostName { get; }

    /// <summary>
    /// Gets the owner of the remote repository.
    /// </summary>
    public abstract string RepositoryOwner { get; }

    /// <summary>
    /// Gets the name of the remote repository.
    /// </summary>
    public abstract string RepositoryName { get; }

    /// <summary>
    /// Gets the URL of the remote repository.
    /// </summary>
    public abstract Uri RepositoryUrl { get; }

    /// <summary>
    /// Gets a value indicating whether the current build is taking place
    /// on a Continuous Integration server.
    /// </summary>
    /// <value>If the build is taking place on a Continuous Integration server, <see langword="true"/>;
    /// otherwise (i.e. if it is a local build), <see langword="false"/>.</value>
    public abstract bool IsCloudBuild { get; }

    /// <summary>
    /// Gets the identity of the bot user, if any, used for CI operations such as release creation.
    /// </summary> <remarks>
    /// <para>Depending on the specific build platform, this property may return <see langword="null"/>
    /// if the platform does not have a specific bot user for CI operations, or if the bot user is not identifiable.</para>
    /// </remarks>
    public abstract GitIdentity? CIBotIdentity { get; }

    /// <summary>
    /// Gets the username used for pushing to the remote repository.
    /// </summary>
    public abstract string? PushUsername { get; }

    /// <summary>
    /// Gets the password used for pushing to the remote repository.
    /// </summary>
    public abstract string? PushPassword { get; }

    /// <summary>
    /// Creates and returns an instance of <see cref="ServerAdapter"/>
    /// suitable for the repository.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <returns>A newly-created instance of <see cref="ServerAdapter"/>.</returns>
    public static ServerAdapter Create(IServiceProvider services)
        => GitHubServerAdapter.CreateIfApplicable(services)
            ?? GitLabServerAdapter.CreateIfApplicable(services)
            ?? new UnknownServerAdapter(services);

    /// <summary>
    /// Asynchronously gets a value that indicates whether the current repository is private.
    /// </summary>
    /// <returns>A Task, representing the ongoing operation, whose result will be <see langword="true"/>
    /// if the current repository is private, <see langword="false"/> if it is public.</returns>
    public abstract Task<bool> IsPrivateRepositoryAsync();

    /// <summary>
    /// Gets the URL of a release.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <returns>The URL of the release identified by <paramref name="version"/>.</returns>
    public abstract Uri GetReleaseUrl(string version);

    /// <summary>
    /// Gets the URL of a file on the server.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="commitish">The SHA or reference to which the file belongs.</param>
    /// <returns></returns>
    public abstract Uri GetFileUrl(FilePath path, string commitish);

    /// <summary>
    /// Asynchronously creates a <see cref="ServerRelease"/> object, based on the current Git branch.
    /// </summary>
    /// <returns>A <see cref="Task"/>, representing the ongoing operation,
    /// whose result will be a newly-created instance of <see cref="ServerRelease"/>
    /// representing the created release.</returns>
    /// <remarks>
    /// <para>Depending on the specific build platform, this method does not necessarily create
    /// a release at once; however, if the platform allows for the creation of draft releases
    /// and their subsequent editing and asset uploading (e.g. GitHub Actions) subclasses
    /// should take advantage of it.</para>
    /// </remarks>
    public abstract Task<ServerRelease> CreateReleaseAsync();
}
