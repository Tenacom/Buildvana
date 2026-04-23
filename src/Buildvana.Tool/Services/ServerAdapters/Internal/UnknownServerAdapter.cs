// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Buildvana.Tool.Utilities;
using Cake.Core;
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
    private readonly ICakeContext _context;

    internal UnknownServerAdapter(IServiceProvider services)
    {
        _context = services.GetRequiredService<ICakeContext>();
    }

    /// <inheritdoc/>
    public override string Name => "(unknown / local build)";

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string HostName => _context.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string RepositoryOwner => _context.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override string RepositoryName => _context.FailOnUnsupportedProperty<string>();

    /// <inheritdoc/>
    /// <summary>
    /// This property is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri RepositoryUrl => _context.FailOnUnsupportedProperty<Uri>();

    /// <inheritdoc/>
    /// <value>Always <see langword="false"/>.</value>
    public override bool IsCloudBuild => false;

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Task<bool> IsPrivateRepositoryAsync() => _context.FailOnUnsupportedMethod<Task<bool>>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetReleaseUrl(string version) => _context.FailOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Uri GetFileUrl(FilePath path, string commitish) => _context.FailOnUnsupportedMethod<Uri>();

    /// <inheritdoc/>
    /// <summary>
    /// This method is not supported on this adapter and will always throw.
    /// </summary>
    public override Task<ServerRelease> CreateReleaseAsync() => _context.FailOnUnsupportedMethod<Task<ServerRelease>>();
}
