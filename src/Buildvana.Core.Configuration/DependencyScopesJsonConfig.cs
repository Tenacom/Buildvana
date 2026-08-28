// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json.Serialization;
using Buildvana.Core.Json.Schema;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>dependencies.scopes</c> section: the update policy governing each dependency scope.
/// </summary>
/// <remarks>
/// <para>Each member takes a policy string of the kind its own position accepts: <c>netsdk</c> the .NET SDK
/// kinds, every other member the package kinds. The schema enumerates both sets, so a kind stated in the
/// wrong position is rejected where it is written.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DependencyScopesJsonConfig
{
    /// <summary>Gets the policy governing the .NET SDK version.</summary>
    [JsonPropertyName("netsdk")]
    [JsonAllowedValues(UpdatePolicySyntax.NetSdkPolicyValues)]
    [Description(
        "How far an automatic update may move the .NET SDK version pinned in global.json. "
        + "A trailing - allows prerelease versions.")]
    public string? NetSdk { get; init; }

    /// <summary>Gets the policy governing the MSBuild project SDKs.</summary>
    [JsonAllowedValues(UpdatePolicySyntax.PackagePolicyValues)]
    [Description(
        "How far an automatic update may move an MSBuild project SDK pinned in global.json. "
        + "A trailing - allows prerelease versions.")]
    public string? Sdks { get; init; }

    /// <summary>Gets the policy governing the .NET local tools.</summary>
    [JsonAllowedValues(UpdatePolicySyntax.PackagePolicyValues)]
    [Description(
        "How far an automatic update may move a .NET local tool pinned in the tool manifest. "
        + "A trailing - allows prerelease versions.")]
    public string? Tools { get; init; }

    /// <summary>Gets the policy governing the NuGet package pins.</summary>
    [JsonAllowedValues(UpdatePolicySyntax.PackagePolicyValues)]
    [Description(
        "How far an automatic update may move a NuGet package pin. A trailing - allows prerelease versions.")]
    public string? Packages { get; init; }
}
