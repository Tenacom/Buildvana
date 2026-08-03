// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Configures version computation.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record VersioningConfig
{
    /// <summary>Gets the prerelease tag applied to prerelease versions.</summary>
    [Description("Prerelease tag applied to prerelease versions. When omitted, prerelease versions are not allowed.")]
    public string? PrereleaseTag { get; init; }

    /// <summary>Gets the assembly-version precision.</summary>
    [Description("How many version components are carried into the assembly version.")]
    public AssemblyVersionPrecision? AssemblyVersionPrecision { get; init; }
}
