// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved dependency-management configuration: which scopes are managed, how far each pin may move,
/// and where package pins are declared beyond the files the <c>packages</c> scope finds by itself.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DependenciesConfig
{
    /// <summary>Gets the update policy of each dependency scope.</summary>
    public DependencyScopesConfig Scopes { get; init; } = new();

    /// <summary>
    /// Gets the per-package policy rules, in the order the configuration file states them: the first rule
    /// whose pattern matches a package id governs its pin. When empty, no pin has a policy of its own.
    /// </summary>
    /// <remarks>
    /// <para>Order is the only rule. There is no ranking by specificity, so a general pattern placed first
    /// silences every later one — deliberately, the order being the user's own.</para>
    /// <para>This is a list, never a dictionary: the configuration file writes it as an object for the
    /// user's convenience, and the order of that object's members is what decides matches.</para>
    /// </remarks>
    public IReadOnlyList<UpdatePolicyRule> Policies { get; init; } = [];

    /// <summary>
    /// Gets the additional pin groups, in the order the configuration file states them. When empty, package
    /// pins are read only from the files the <c>packages</c> scope finds by itself.
    /// </summary>
    public IReadOnlyList<AdditionalPackagesConfig> AdditionalPackages { get; init; } = [];
}
