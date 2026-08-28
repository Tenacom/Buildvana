// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// One entry of <see cref="DependenciesConfig.Policies"/>: a pattern over package ids, and the update
/// policy governing every pin whose id it matches.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UpdatePolicyRule
{
    /// <summary>
    /// Gets the pattern matched against a package id: literal text, with <c>*</c> standing for any run of
    /// characters. The whole id must match, and matching is case-insensitive, package ids being so.
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// Gets the update policy governing a matching pin, as a package policy string; see
    /// <see cref="DependencyScopesConfig"/> for the syntax.
    /// </summary>
    public required string Policy { get; init; }
}
