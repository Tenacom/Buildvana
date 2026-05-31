// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json.Serialization;
using Buildvana.Core.JsonSchema;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Represents the contents of a <c>buildvana.json</c> / <c>buildvana.jsonc</c> configuration file.
/// </summary>
/// <remarks>
/// <para>Every member is optional; an absent configuration file is equivalent to an instance with all members unset.</para>
/// </remarks>
[JsonSchemaTitle("Buildvana configuration")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record BuildvanaConfig
{
    /// <summary>Gets the URI of the JSON schema describing this file.</summary>
    /// <remarks>
    /// <para>This member exists only so that a <c>$schema</c> reference does not trip unmapped-member rejection;
    /// it carries no configuration meaning.</para>
    /// </remarks>
    [JsonPropertyName("$schema")]
    [Description("URI of the JSON schema describing this file.")]
    public string? Schema { get; init; }

    /// <summary>Gets the release-workflow configuration.</summary>
    [Description("Configuration for the bv release workflow.")]
    public ReleaseConfig? Release { get; init; }

    /// <summary>Gets the version-computation configuration.</summary>
    [Description("Configuration for version computation.")]
    public VersioningConfig? Versioning { get; init; }

    /// <summary>Gets the dotnet CLI configuration.</summary>
    [JsonPropertyName("dotnet")]
    [Description("Configuration for invocations of the dotnet CLI.")]
    public DotNetConfig? DotNet { get; init; }

    /// <summary>Gets the NuGet publishing configuration.</summary>
    [JsonPropertyName("nuget")]
    [Description("Configuration for NuGet package publishing.")]
    public NuGetConfig? NuGet { get; init; }

    /// <summary>Gets the GitHub integration configuration.</summary>
    [JsonPropertyName("github")]
    [Description("Configuration for GitHub integration.")]
    public GitHubConfig? GitHub { get; init; }

    /// <summary>Gets the Git configuration.</summary>
    [Description("Configuration for Git-related behavior.")]
    public GitConfig? Git { get; init; }
}
