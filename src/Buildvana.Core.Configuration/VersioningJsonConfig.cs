// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using Buildvana.Core.Json.Schema;
using Buildvana.Runtime;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>versioning</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record VersioningJsonConfig
{
    /// <summary>Gets the prerelease tag applied to prerelease versions.</summary>
    [JsonSchemaExample("\"preview\"")]
    [Description("Prerelease tag applied to prerelease versions. When omitted, prerelease versions are not allowed.")]
    public string? PrereleaseTag { get; init; }

    /// <summary>Gets the assembly-version precision.</summary>
    [Description("How many version components are carried into the assembly version.")]
    public AssemblyVersionPrecision? AssemblyVersionPrecision { get; init; }
}
