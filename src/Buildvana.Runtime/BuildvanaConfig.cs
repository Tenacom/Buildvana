// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved Buildvana configuration of a run: every setting at its effective value, with the
/// configuration file, the command line, and the built-in defaults already composed.
/// </summary>
/// <remarks>
/// <para>Every default lives here, as a property initializer on the record that owns the setting: a bare
/// <c>new BuildvanaConfig()</c> is the effective configuration of a repository that configures nothing.</para>
/// <para>In this model, a nullable member is a domain option whose <see langword="null"/> has exactly one,
/// documented meaning — never "unspecified". Resolution has already happened; no consumer falls back to
/// anything.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record BuildvanaConfig
{
    /// <summary>Gets the resolved release-workflow configuration.</summary>
    public ReleaseConfig Release { get; init; } = new();

    /// <summary>Gets the resolved version-computation configuration.</summary>
    public VersioningConfig Versioning { get; init; } = new();

    /// <summary>Gets the resolved dotnet CLI configuration.</summary>
    [JsonPropertyName("dotnet")]
    public DotNetConfig DotNet { get; init; } = new();

    /// <summary>Gets the resolved NuGet publishing configuration.</summary>
    [JsonPropertyName("nuget")]
    public NuGetConfig NuGet { get; init; } = new();

    /// <summary>Gets the resolved GitHub integration configuration.</summary>
    [JsonPropertyName("github")]
    public GitHubConfig GitHub { get; init; } = new();

    /// <summary>Gets the resolved Git configuration.</summary>
    public GitConfig Git { get; init; } = new();

    /// <summary>Gets the resolved dependency-management configuration.</summary>
    public DependenciesConfig Dependencies { get; init; } = new();

    /// <summary>
    /// Gets the gitignore-syntax patterns selecting the files and directories that hold file-based C# apps:
    /// the built-in hooks directory, plus the patterns the configuration file adds. Commands that scan for
    /// <c>#:</c> directives read only <c>.cs</c> files within this scope.
    /// </summary>
    public IReadOnlyList<string> FileBasedApps { get; init; } = [WellKnownPaths.HooksDirectory + "/"];
}
