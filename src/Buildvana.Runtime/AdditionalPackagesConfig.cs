// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// One entry of <see cref="DependenciesConfig.AdditionalPackages"/>: a group of package pins declared in
/// repository files of its own, beyond the ones the <c>packages</c> scope finds by itself.
/// </summary>
/// <remarks>
/// <para>Items of an additional group are assumed to carry the same metadata as <c>PackageVersion</c> items.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record AdditionalPackagesConfig
{
    /// <summary>
    /// Gets the group's caption: its display name in listings, and the key it is declared under in the
    /// configuration file, which makes it unique among groups.
    /// </summary>
    public required string Caption { get; init; }

    /// <summary>
    /// Gets the glob, relative to the home directory, selecting the files that declare the group's pins.
    /// </summary>
    public required string Files { get; init; }

    /// <summary>Gets the MSBuild item name the group's pins are declared as, e.g. <c>BV_PackageVersion</c>.</summary>
    public required string Items { get; init; }

    /// <summary>
    /// Gets the update policy governing the group's pins, as a package policy string; see
    /// <see cref="DependencyScopesConfig"/> for the syntax.
    /// </summary>
    /// <remarks>
    /// <para>A group that states no policy of its own resolves to <see cref="DependencyScopesConfig.Packages"/>,
    /// so this member always names an actual policy. A pin matched by a <see cref="UpdatePolicyRule"/>, or
    /// carrying <c>UpdatePolicy</c> metadata, is governed by that instead: both outrank a group policy.</para>
    /// </remarks>
    public required string Policy { get; init; }
}
