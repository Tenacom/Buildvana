// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Buildvana.Core.Configuration;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Everything the choice of an override for one vulnerable package of one project depends on.
/// </summary>
internal sealed record OverrideRequest
{
    /// <summary>Gets the version the project's restore resolved.</summary>
    public required NuGetVersion ResolvedVersion { get; init; }

    /// <summary>
    /// Gets the versions of the package the sources list. A delisted version is not among them: its author
    /// hid it, often for the very reason an override is being written.
    /// </summary>
    public required IReadOnlyCollection<NuGetVersion> Candidates { get; init; }

    /// <summary>Gets every advisory the sources know for the package, of every severity.</summary>
    public required IReadOnlyList<PackageAdvisory> Advisories { get; init; }

    /// <summary>Gets the severity the project's audit reports from, which is its <c>NuGetAuditLevel</c>.</summary>
    public required PackageVulnerabilitySeverity AuditLevel { get; init; }

    /// <summary>Gets the package's effective update policy.</summary>
    public required PackageUpdatePolicy Policy { get; init; }

    /// <summary>
    /// Gets a value indicating whether the project references the package itself. A reference a sidecar file
    /// promoted is bv's own and is not one of these.
    /// </summary>
    public bool IsDirectReference { get; init; }

    /// <summary>
    /// Gets the version the repository pins the package at centrally, or <see langword="null"/> where it
    /// pins none, or where the project does not manage its versions centrally.
    /// </summary>
    public NuGetVersion? CentralPin { get; init; }
}
