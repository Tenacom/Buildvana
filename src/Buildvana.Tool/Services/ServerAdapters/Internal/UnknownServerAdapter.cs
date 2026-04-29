// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Services.Git;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Buildvana.Tool.Services.ServerAdapters.Internal;

/// <summary>
/// <para>Implements a dummy Continuous Integration adapter for unknown system / local build.</para>
/// <para>All property and methods of this class will fail the build when called,
/// except for <see cref="IsCloudBuild"/>, which will always return <see langword="false"/>.</para>
/// </summary>
internal sealed class UnknownServerAdapter : ServerAdapter
{
    private readonly IBuildHost _host;

    internal UnknownServerAdapter(IServiceProvider services)
    {
        _host = services.GetRequiredService<IBuildHost>();
    }

    /// <inheritdoc/>
    public override string Name => "(unknown / local build)";

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string HostName => _host.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string RepositoryOwner => _host.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string RepositoryName => _host.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri RepositoryUrl => _host.FailOnUnsupportedProperty<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override GitIdentity CIBotIdentity => _host.FailOnUnsupportedProperty<GitIdentity>();

    /// <inheritdoc/>
    public override string? PushUsername => null;

    /// <inheritdoc/>
    public override string? PushPassword => null;

    /// <inheritdoc/>
    /// <value>Always <see langword="false"/>.</value>
    public override bool IsCloudBuild => false;

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Task<bool> IsPrivateRepositoryAsync() => _host.FailOnUnsupportedMethod<Task<bool>>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetReleaseUrl(string version) => _host.FailOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetFileUrl(FilePath path, string commitish) => _host.FailOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Task<ServerRelease> CreateReleaseAsync() => _host.FailOnUnsupportedMethod<Task<ServerRelease>>();
}
