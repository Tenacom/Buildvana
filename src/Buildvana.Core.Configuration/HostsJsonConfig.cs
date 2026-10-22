// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>hosts</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record HostsJsonConfig
{
    /// <summary>Gets the <c>hosts.github</c> section.</summary>
    [JsonPropertyName("github")]
    [Description("Configuration for the GitHub host.")]
    public GitHubJsonConfig? GitHub { get; init; }
}
