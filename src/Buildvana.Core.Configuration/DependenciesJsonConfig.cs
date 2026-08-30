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
    [Description("Update policy of packages matching an id pattern. The first match wins.")]
    public IReadOnlyList<UpdatePolicyRuleJsonConfig>? Policies { get; init; }

    /// <summary>Gets the <c>additionalPackages</c> entries, in document order.</summary>
    [Description("Groups of package pins declared in their own files, keyed by caption.")]
    public IReadOnlyList<AdditionalPackagesJsonConfig>? AdditionalPackages { get; init; }
}
