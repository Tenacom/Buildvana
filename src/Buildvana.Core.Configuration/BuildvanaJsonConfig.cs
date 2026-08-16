// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text.Json.Serialization;
using Buildvana.Core.JsonSchema;
using Buildvana.Runtime;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the contents of a <c>buildvana.json</c> / <c>buildvana.jsonc</c> configuration file:
/// the wire model of the file, faithful to its format.
/// </summary>
/// <remarks>
/// <para>In a wire model, <see langword="null"/> has exactly one meaning: the member is not stated in the file.
/// Wire models carry no defaults and resolve nothing; they only say what was written.</para>
/// </remarks>
[JsonSchemaTitle("Buildvana configuration")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record BuildvanaJsonConfig
{
    /// <summary>
    /// The name of the configuration file in plain JSON form. The file lives in the home directory itself:
    /// a configuration file elsewhere, <see cref="WellKnownPaths.BuildvanaDirectory"/> included, is not one.
    /// </summary>
    public const string JsonFileName = "buildvana.json";

    /// <summary>
    /// The name of the configuration file in JSON-with-comments form, subject to the same
    /// single candidate location as <see cref="JsonFileName"/>.
    /// </summary>
    public const string JsoncFileName = "buildvana.jsonc";

    /// <summary>Gets the URI of the JSON schema describing this file.</summary>
    /// <remarks>
    /// <para>This member exists only so that a <c>$schema</c> reference does not trip unmapped-member rejection;
    /// it carries no configuration meaning and — being wire-only — has no domain counterpart, so it opts out
    /// of default annotation.</para>
    /// </remarks>
    [JsonPropertyName("$schema")]
    [JsonSchemaNoDefault]
    [Description("URI of the JSON schema describing this file.")]
    public string? Schema { get; init; }

    /// <summary>Gets the <c>release</c> section.</summary>
    [Description("Configuration for the bv release workflow.")]
    public ReleaseJsonConfig? Release { get; init; }

    /// <summary>Gets the <c>versioning</c> section.</summary>
    [Description("Configuration for version computation.")]
    public VersioningJsonConfig? Versioning { get; init; }

    /// <summary>Gets the <c>dotnet</c> section.</summary>
    [JsonPropertyName("dotnet")]
    [Description("Configuration for invocations of the dotnet CLI.")]
    public DotNetJsonConfig? DotNet { get; init; }

    /// <summary>Gets the <c>nuget</c> section.</summary>
    [JsonPropertyName("nuget")]
    [Description("Configuration for NuGet package publishing.")]
    public NuGetJsonConfig? NuGet { get; init; }

    /// <summary>Gets the <c>github</c> section.</summary>
    [JsonPropertyName("github")]
    [Description("Configuration for GitHub integration.")]
    public GitHubJsonConfig? GitHub { get; init; }

    /// <summary>Gets the <c>git</c> section.</summary>
    [Description("Configuration for Git-related behavior.")]
    public GitJsonConfig? Git { get; init; }
}
