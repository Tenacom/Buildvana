// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>dependencies</c> section: how far each dependency may move when it is updated, and where
/// package pins are declared beyond the files the <c>packages</c> scope finds by itself.
/// </summary>
/// <remarks>
/// <para>Both lists are written as JSON objects, whose member names are data: a pattern for
/// <see cref="Policies"/>, a caption for <see cref="AdditionalPackages"/>. The shape spares the user a
/// wrapper object per entry and makes the names unique by construction, while document order — which
/// decides which policy claims a pin — is preserved.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DependenciesJsonConfig
{
    /// <summary>Gets the <c>scopes</c> section.</summary>
    [Description("Update policy governing each dependency scope.")]
    public DependencyScopesJsonConfig? Scopes { get; init; }

    /// <summary>Gets the <c>policies</c> entries, in document order.</summary>
    [Description(
        "Update policy of individual packages, keyed by a pattern matched against a whole package id, "
        + "with * standing for any run of characters. The first matching entry wins, so specific patterns "
        + "go before general ones. Entries govern the sdks, tools, and packages scopes alike, and outrank "
        + "the policy of an additional package group.")]
    public IReadOnlyList<UpdatePolicyRuleJsonConfig>? Policies { get; init; }

    /// <summary>Gets the <c>additionalPackages</c> entries, in document order.</summary>
    [Description(
        "Groups of package pins declared in files of their own, keyed by the caption naming the group in "
        + "listings. Pins of a group are updated like any other package pin, but are never pruned and never "
        + "given transitive overrides.")]
    public IReadOnlyList<AdditionalPackagesJsonConfig>? AdditionalPackages { get; init; }
}
