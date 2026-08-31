// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Answers which .NET SDK releases exist, and which of them belong to a long-term support channel.
/// </summary>
/// <remarks>
/// <para>The <c>netsdk</c> scope resolves against this rather than against a package source: a .NET SDK is
/// not a NuGet package, and whether a release is long-term support is a fact only its channel states.</para>
/// </remarks>
internal interface INetSdkReleaseSource
{
    /// <summary>
    /// Reads the releases that could matter to a pinned version.
    /// </summary>
    /// <param name="pinnedVersion">The pinned .NET SDK version.</param>
    /// <param name="cancellationToken">A token that, when signalled, abandons the reading.</param>
    /// <returns>Every release of the channel the pinned version belongs to, and of every newer channel. A
    /// release older than that channel is left out: it can neither be a target, since no update lowers a
    /// pin, nor be worth reporting as what lies beyond the policy.</returns>
    /// <exception cref="BuildFailedException">The release index could not be read.</exception>
    Task<IReadOnlyList<NetSdkRelease>> GetReleasesAsync(NuGetVersion pinnedVersion, CancellationToken cancellationToken = default);
}
