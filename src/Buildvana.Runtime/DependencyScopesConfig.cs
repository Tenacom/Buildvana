// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved update policy of each dependency scope: what governs a pin of that scope when nothing more
/// specific claims it.
/// </summary>
/// <remarks>
/// <para>A policy is stated as a policy string: a lowercase kind name, optionally followed by <c>-</c>
/// meaning that prerelease versions are candidates too. The kind names how far an automatic update may move
/// a pin. Every scope but <c>netsdk</c> takes a package kind — <c>disable</c>, <c>exact</c>,
/// <c>revision</c>, <c>patch</c>, <c>minor</c>, or <c>major</c>; <c>netsdk</c> takes a .NET SDK kind —
/// <c>disable</c>, <c>patch</c>, <c>feature</c>, <c>minor</c>, <c>major</c>, or <c>lts</c>. The two
/// vocabularies differ because .NET SDK versions are not SemVer.</para>
/// <para>Every value here has already been validated against the kind its position accepts, so a consumer
/// parses rather than checks.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DependencyScopesConfig
{
    /// <summary>
    /// Gets the policy governing the .NET SDK version pinned in the <c>sdk</c> section of
    /// <c>global.json</c>.
    /// </summary>
    [JsonPropertyName("netsdk")]
    public string NetSdk { get; init; } = "major";

    /// <summary>
    /// Gets the policy governing the MSBuild project SDKs pinned in the <c>msbuild-sdks</c> section of
    /// <c>global.json</c>.
    /// </summary>
    public string Sdks { get; init; } = "minor";

    /// <summary>Gets the policy governing the .NET local tools pinned in the tool manifest.</summary>
    public string Tools { get; init; } = "minor";

    /// <summary>Gets the policy governing the NuGet package pins, additional pin groups included.</summary>
    public string Packages { get; init; } = "minor";
}
