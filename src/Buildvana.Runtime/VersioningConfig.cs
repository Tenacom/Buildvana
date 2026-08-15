// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved version-computation configuration.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record VersioningConfig
{
    /// <summary>
    /// Gets the prerelease tag applied to prerelease versions, or <see langword="null"/> when prerelease
    /// versions are not allowed.
    /// </summary>
    public string? PrereleaseTag { get; init; }

    /// <summary>Gets how many version components are carried into the assembly version.</summary>
    public AssemblyVersionPrecision AssemblyVersionPrecision { get; init; } = AssemblyVersionPrecision.Major;
}
