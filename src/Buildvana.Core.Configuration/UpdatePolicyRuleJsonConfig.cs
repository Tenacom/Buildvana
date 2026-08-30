// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using Buildvana.Core.Json;
using Buildvana.Core.Json.Schema;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models one member of the <c>dependencies.policies</c> object: a pattern over package ids, written as the
/// member name, and the policy governing every pin whose id it matches, written as the member value.
/// </summary>
[JsonKeyedObject(nameof(Pattern), nameof(Policy))]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UpdatePolicyRuleJsonConfig
{
    /// <summary>Gets the pattern, which is the member name.</summary>
    [JsonSchemaExample("\"Some.Package.*\"")]
    public required string Pattern { get; init; }

    /// <summary>Gets the policy, which is the member value.</summary>
    [JsonAllowedValues(UpdatePolicySyntax.PackagePolicyValues)]
    [JsonSchemaExample("\"patch\"")]
    [Description(
        "How far an automatic update may move a pin whose package id matches. "
        + "A trailing - allows prerelease versions.")]
    public required string Policy { get; init; }
}
