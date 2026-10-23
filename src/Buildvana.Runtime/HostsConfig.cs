// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved configuration of the Git hosts, one section per kind of host.
/// </summary>
/// <remarks>
/// <para>Each host reads its own section alone, so a configuration can state the sections of several hosts.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record HostsConfig
{
    /// <summary>Gets the resolved GitHub configuration.</summary>
    [JsonPropertyName("github")]
    public GitHubConfig GitHub { get; init; } = new();

    /// <summary>Gets the resolved GitLab configuration.</summary>
    [JsonPropertyName("gitlab")]
    public GitLabConfig GitLab { get; init; } = new();
}
