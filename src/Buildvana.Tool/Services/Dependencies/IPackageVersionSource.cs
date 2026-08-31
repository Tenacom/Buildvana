// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Answers what the repository's package sources know about a package id.
/// </summary>
/// <remarks>
/// <para>This is the one boundary between <c>bv dependencies</c> and the network. Everything a run decides
/// about an id-shaped pin — whether it exists, whether it is listed, where its policy may take it — is
/// decided from what this returns.</para>
/// </remarks>
internal interface IPackageVersionSource
{
    /// <summary>
    /// Gets the names of the enabled package sources, in configuration order. A repository with no enabled
    /// source has none, and nothing can be resolved against them.
    /// </summary>
    IReadOnlyList<string> Sources { get; }

    /// <summary>
    /// Reads what the sources know about a package id.
    /// </summary>
    /// <param name="packageId">The package id to look up.</param>
    /// <param name="cancellationToken">A token that, when signalled, abandons the lookup.</param>
    /// <returns>What the sources know; <see cref="PackageVersionCatalog.Empty"/> when none of them knows the
    /// id at all.</returns>
    /// <exception cref="BuildFailedException">A source could not be reached.</exception>
    Task<PackageVersionCatalog> GetVersionsAsync(string packageId, CancellationToken cancellationToken = default);
}
