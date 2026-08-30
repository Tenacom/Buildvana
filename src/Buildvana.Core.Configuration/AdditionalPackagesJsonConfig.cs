// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using Buildvana.Core.Json;
using Buildvana.Core.Json.Schema;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models one member of the <c>dependencies.additionalPackages</c> object: a group of package pins declared
/// in files of its own, under the caption that names it in listings.
/// </summary>
[JsonKeyedObject(nameof(Caption))]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record AdditionalPackagesJsonConfig
{
    /// <summary>Gets the group's caption, which is the member name.</summary>
    [JsonSchemaExample("\"SDK package injections\"")]
    public required string Caption { get; init; }

    /// <summary>Gets the glob selecting the files that declare the group's pins.</summary>
    [JsonSchemaExample("\"path/to/MyPackages.props\"")]
    [Description("Glob, relative to the home directory, selecting the files that declare the group's pins.")]
    public required string Files { get; init; }

    /// <summary>Gets the MSBuild item name the group's pins are declared as.</summary>
    [JsonSchemaExample("\"PackageVersion\"")]
    [Description("MSBuild item name the group's pins are declared as, e.g. BV_PackageVersion.")]
    public required string Items { get; init; }

    /// <summary>Gets the policy governing the group's pins, or <see langword="null"/> when unstated.</summary>
    [JsonAllowedValues(UpdatePolicySyntax.PackagePolicyValues)]
    [JsonSchemaExample("\"minor\"")]
    [Description(
        "How far an automatic update may move a pin of this group. A trailing - allows prerelease versions. "
        + "Defaults to the policy of the packages scope.")]
    public string? Policy { get; init; }
}
