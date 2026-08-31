// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.Dependencies;

/// <summary>
/// One package item of a <see cref="PackagePinDump"/>: its type, its identity, and the metadata that decides
/// whether <c>bv</c> manages it and how far it may move.
/// </summary>
/// <remarks>
/// <para>Every member but <see cref="ItemType"/>, <see cref="Id"/> and <see cref="DefiningProjectFullPath"/>
/// mirrors an MSBuild metadatum, which is a string that is either stated or absent. An absent metadatum
/// reads as <see langword="null"/> here, never as an empty string.</para>
/// <para>The dump states what the evaluation found, and judges none of it: an item the tool drops on sight,
/// an implicitly defined one for instance, is dumped like any other.</para>
/// </remarks>
public sealed record PackagePinDumpItem
{
    /// <summary>
    /// Gets the item type the item was declared as: <c>PackageVersion</c>, <c>GlobalPackageReference</c>, or
    /// <c>PackageReference</c>.
    /// </summary>
    public required string ItemType { get; init; }

    /// <summary>Gets the item's identity, which is the package id.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the evaluated value of the item's <c>Version</c> metadatum, or <see langword="null"/> when the
    /// item states none.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the evaluated value of the item's <c>VersionOverride</c> metadatum, or <see langword="null"/>
    /// when the item states none.
    /// </summary>
    public string? VersionOverride { get; init; }

    /// <summary>
    /// Gets the evaluated value of the item's <c>UpdatePolicy</c> metadatum, which is the policy the pin
    /// states for itself, or <see langword="null"/> when the item states none.
    /// </summary>
    public string? UpdatePolicy { get; init; }

    /// <summary>
    /// Gets a value indicating whether the item carries <c>IsImplicitlyDefined</c> metadata reading
    /// <c>true</c>, which marks a reference an SDK injects rather than one the repository declares.
    /// </summary>
    public bool IsImplicitlyDefined { get; init; }

    /// <summary>
    /// Gets the full path of the file that declares the item, which is MSBuild's own
    /// <c>DefiningProjectFullPath</c> metadatum.
    /// </summary>
    public required string DefiningProjectFullPath { get; init; }
}
