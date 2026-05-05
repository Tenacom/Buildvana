// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Services.Git;

namespace Buildvana.Tool.Services.ServerAdapters.Internal;

/// <summary>
/// <para>Implements a dummy Continuous Integration adapter for unknown system / local build.</para>
/// <para>All property and methods of this class will fail the build when called,
/// except for <see cref="IsCloudBuild"/>, which will always return <see langword="false"/>.</para>
/// </summary>
internal sealed class UnknownServerAdapter : ServerAdapter
{
    /// <inheritdoc/>
    public override string Name => "(unknown / local build)";

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string HostName => BuildFailedException.ThrowOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string RepositoryOwner => BuildFailedException.ThrowOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string RepositoryName => BuildFailedException.ThrowOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri RepositoryUrl => BuildFailedException.ThrowOnUnsupportedProperty<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override GitIdentity CIBotIdentity => BuildFailedException.ThrowOnUnsupportedProperty<GitIdentity>();

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
    public override Task<bool> IsPrivateRepositoryAsync() => BuildFailedException.ThrowOnUnsupportedMethod<Task<bool>>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetReleaseUrl(string version) => BuildFailedException.ThrowOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetFileUrl(string path, string commitish) => BuildFailedException.ThrowOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Task<ServerRelease> CreateReleaseAsync() => BuildFailedException.ThrowOnUnsupportedMethod<Task<ServerRelease>>();
}
