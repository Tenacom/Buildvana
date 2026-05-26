// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Services.Versioning;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>release</c> command, parsed from the command-line option tokens by <see cref="Parse"/>.
/// Decorated with <see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/> for the help renderer.
/// </summary>
internal sealed class ReleaseSettings
{
    /// <summary>
    /// Gets the MSBuild configuration to build.
    /// </summary>
    [BvOption("-c|--configuration <NAME>")]
    [Description("MSBuild configuration to build. Defaults to 'Release'.")]
    public string? Configuration { get; init; }

    /// <summary>
    /// Gets the requested version-spec change.
    /// </summary>
    [BvOption("--bump <CHANGE>")]
    [Description("""
        Version-spec change to apply:
          - [bold]none[/] (the default): advance patch from Git height.
          - [bold]unstable[/]: advance patch, add prerelease label.
          - [bold]stable[/]: advance patch, drop prerelease label.
          - [bold]minor[/]: advance minor, reset patch, add prerelease label.
          - [bold]major[/]: advance major, reset minor and patch, add prerelease label.
        """)]
    public string? Bump { get; init; }

    /// <summary>
    /// Gets a value indicating whether the public API is checked when computing version-spec changes.
    /// </summary>
    [BvOption("--check-public-api <BOOL>")]
    [Description("Check the public API when computing version-spec changes. Defaults to true.")]
    public bool? CheckPublicApi { get; init; }

    /// <summary>
    /// Gets a value indicating whether the changelog is updated on unstable (prerelease) versions.
    /// </summary>
    [BvOption("--unstable-changelog <BOOL>")]
    [Description("Update the changelog on unstable (prerelease) versions. Defaults to false.")]
    public bool? UnstableChangelog { get; init; }

    /// <summary>
    /// Gets a value indicating whether the build is failed if the 'Unreleased changes' section is empty.
    /// </summary>
    [BvOption("--require-changelog <BOOL>")]
    [Description("Fail the build if the 'Unreleased changes' section is empty. Defaults to true.")]
    public bool? RequireChangelog { get; init; }

    /// <summary>
    /// Gets a value indicating whether in-tree references to packages produced by this release are updated.
    /// </summary>
    [BvOption("--dogfood <BOOL>")]
    [Description("Update in-tree references to packages produced by this release. Defaults to true.")]
    public bool? Dogfood { get; init; }

    /// <summary>
    /// Parses the command's option tokens into a <see cref="ReleaseSettings"/>, rejecting any option the command
    /// does not recognize.
    /// </summary>
    /// <param name="options">The option tokens for the <c>release</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">An option value is invalid, or an unrecognized option was given.</exception>
    public static ReleaseSettings Parse(IReadOnlyList<string> options)
    {
        var reader = new CliOptionReader(options);
        var settings = new ReleaseSettings
        {
            Configuration = reader.ReadValue("--configuration", "-c"),
            Bump = reader.ReadValue("--bump"),
            CheckPublicApi = ParseBool(reader.ReadValue("--check-public-api"), "--check-public-api"),
            UnstableChangelog = ParseBool(reader.ReadValue("--unstable-changelog"), "--unstable-changelog"),
            RequireChangelog = ParseBool(reader.ReadValue("--require-changelog"), "--require-changelog"),
            Dogfood = ParseBool(reader.ReadValue("--dogfood"), "--dogfood"),
        };

        if (reader.Remaining.Count > 0)
        {
            throw new BuildFailedException($"Unknown option '{reader.Remaining[0]}' for command 'release'.");
        }

        return settings;
    }

    /// <summary>
    /// Parses <see cref="Bump"/> into a <see cref="VersionSpecChange"/>; defaults to <see cref="VersionSpecChange.None"/>.
    /// </summary>
    /// <exception cref="BuildFailedException">The value of <see cref="Bump"/> is not a recognized version-spec change.</exception>
    public VersionSpecChange ResolveBump()
    {
        if (Bump is null)
        {
            return VersionSpecChange.None;
        }

        var parsed = Enum.TryParse<VersionSpecChange>(Bump, ignoreCase: true, out var value) && Enum.IsDefined(value);
        return parsed
            ? value
            : throw new BuildFailedException($"Invalid value '{Bump}' for --bump. Valid values: none, unstable, stable, minor, major.");
    }

    /// <summary>
    /// Gets the resolved MSBuild configuration: <see cref="Configuration"/> if set, otherwise <c>"Release"</c>.
    /// </summary>
    public string ResolveConfiguration() => Configuration ?? "Release";

    /// <summary>
    /// Returns <see cref="CheckPublicApi"/> if set, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveCheckPublicApi() => CheckPublicApi ?? true;

    /// <summary>
    /// Returns <see cref="UnstableChangelog"/> if set, otherwise <see langword="false"/>.
    /// </summary>
    public bool ResolveUnstableChangelog() => UnstableChangelog ?? false;

    /// <summary>
    /// Returns <see cref="RequireChangelog"/> if set, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveRequireChangelog() => RequireChangelog ?? true;

    /// <summary>
    /// Returns <see cref="Dogfood"/> if set, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveDogfood() => Dogfood ?? true;

    private static bool? ParseBool(string? raw, string optionName)
    {
        if (raw is null)
        {
            return null;
        }

        if (bool.TryParse(raw, out var value))
        {
            return value;
        }

        throw new BuildFailedException($"Invalid value '{raw}' for {optionName}. Expected 'true' or 'false'.");
    }
}
