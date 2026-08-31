// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What the configured package sources know about one package id: the versions they list, and the versions
/// they know and have delisted.
/// </summary>
/// <remarks>
/// <para>Only a listed version is a candidate for an update. A delisted version is often a vulnerable one, so
/// moving a pin onto one would be perverse, while a pin already stating one is a finding of its own.</para>
/// <para>Versions compare by precedence, not by text, so a pin reading <c>13.0</c> is found among sources
/// listing <c>13.0.0</c>.</para>
/// <para>Both lists are ordered by precedence, lowest first, and hold no version twice. A version one source
/// lists and another has delisted counts as listed: a restore would accept it.</para>
/// </remarks>
internal sealed record PackageVersionCatalog
{
    /// <summary>Gets the catalog of a package id no configured source knows.</summary>
    public static PackageVersionCatalog Empty { get; } = new() { Listed = [], Unlisted = [] };

    /// <summary>Gets the versions the sources list.</summary>
    public required IReadOnlyList<NuGetVersion> Listed { get; init; }

    /// <summary>Gets the versions the sources know and have delisted.</summary>
    public required IReadOnlyList<NuGetVersion> Unlisted { get; init; }

    /// <summary>
    /// Tells whether any configured source knows the version, listed or delisted.
    /// </summary>
    /// <param name="version">The version to look for.</param>
    /// <returns><see langword="true"/> if a source knows the version; otherwise, <see langword="false"/>.</returns>
    public bool Knows(NuGetVersion version) => IsListed(version) || Contains(Unlisted, version);

    /// <summary>
    /// Tells whether the sources list the version.
    /// </summary>
    /// <param name="version">The version to look for.</param>
    /// <returns><see langword="true"/> if a source lists the version; otherwise, <see langword="false"/>.</returns>
    public bool IsListed(NuGetVersion version) => Contains(Listed, version);

    private static bool Contains(IReadOnlyList<NuGetVersion> versions, NuGetVersion version)
        => versions.Any(known => VersionComparer.VersionRelease.Equals(known, version));
}
