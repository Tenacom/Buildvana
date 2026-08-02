// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.Versioning;
using Buildvana.Tool.CommandLine;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>version advance</c> command. The argument and flag values are parsed from the command's
/// positional and option tokens by <see cref="Parse"/>; each <c>Resolve*</c> method then merges the parsed value
/// with the relevant configuration and a hardcoded default (flag → config → default).
/// Decorated with <see cref="BvArgumentAttribute"/>/<see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/>
/// for the help renderer.
/// </summary>
internal sealed class VersionAdvanceSettings
{
    private readonly ReleaseConfig? _releaseConfig;

    private VersionAdvanceSettings(ReleaseConfig? releaseConfig)
    {
        _releaseConfig = releaseConfig;
    }

    /// <summary>
    /// Gets the requested version-spec change.
    /// </summary>
    [BvArgument("[CHANGE]")]
    [Description("""
        Version-spec change to apply:
          - [bold]none[/] (the default): no change beyond what the analysis requires.
          - [bold]unstable[/]: add prerelease label.
          - [bold]stable[/]: drop prerelease label.
          - [bold]minor[/]: advance minor, add prerelease label.
          - [bold]major[/]: advance major, reset minor, add prerelease label.
        """)]
    public string? Change { get; init; }

    /// <summary>
    /// Gets a value indicating whether the public API is checked when computing the version-spec change.
    /// </summary>
    [BvOption("--check-public-api <BOOL>")]
    [Description("Check the public API when computing the version-spec change. Defaults to true.")]
    public bool? CheckPublicApi { get; init; }

    /// <summary>
    /// Gets a value indicating whether CHANGE is applied verbatim, skipping the version-spec change analysis.
    /// </summary>
    [BvOption("--force")]
    [Description("Apply CHANGE verbatim, skipping the analysis of published versions and public API.")]
    public bool Force { get; init; }

    /// <summary>
    /// Parses the command's positional and option tokens into a <see cref="VersionAdvanceSettings"/>, rejecting
    /// anything the command does not recognize, and binds the configuration sources consulted by the
    /// <c>Resolve*</c> methods.
    /// </summary>
    /// <param name="positionals">The positional tokens for the <c>version advance</c> command (from <c>CommandParameters.Positionals</c>).</param>
    /// <param name="options">The option tokens for the <c>version advance</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <param name="config">The Buildvana configuration whose <c>release</c> section layers between the flags and the defaults.</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">An option value is invalid, or an unrecognized argument or option was given.</exception>
    public static VersionAdvanceSettings Parse(IReadOnlyList<string> positionals, IReadOnlyList<string> options, BuildvanaConfig config)
    {
        Guard.IsNotNull(positionals);
        Guard.IsNotNull(options);
        Guard.IsNotNull(config);
        if (positionals.Count > 1)
        {
            throw new BuildFailedException($"Unexpected argument '{positionals[1]}' for command 'version advance'.");
        }

        var reader = new CliOptionReader(options);
        var settings = new VersionAdvanceSettings(config.Release)
        {
            Change = positionals.Count > 0 ? positionals[0] : null,
            CheckPublicApi = ParseBool(reader.ReadValue("--check-public-api"), "--check-public-api"),
            Force = reader.ReadFlag("--force"),
        };

        if (reader.Remaining.Count > 0)
        {
            throw new BuildFailedException($"Unknown option '{reader.Remaining[0]}' for command 'version advance'.");
        }

        return settings;
    }

    /// <summary>
    /// Parses <see cref="Change"/> into a <see cref="VersionSpecChange"/>; defaults to <see cref="VersionSpecChange.None"/>.
    /// </summary>
    /// <exception cref="BuildFailedException">The value of <see cref="Change"/> is not a recognized version-spec change.</exception>
    public VersionSpecChange ResolveChange()
    {
        if (Change is null)
        {
            return VersionSpecChange.None;
        }

        var parsed = Enum.TryParse<VersionSpecChange>(Change, ignoreCase: true, out var value) && Enum.IsDefined(value);
        return parsed
            ? value
            : throw new BuildFailedException($"Invalid value '{Change}' for CHANGE. Valid values: none, unstable, stable, minor, major.");
    }

    /// <summary>
    /// Returns <see cref="CheckPublicApi"/> if set, otherwise <c>release.checkPublicApi</c>, otherwise <see langword="true"/>.
    /// </summary>
    public bool ResolveCheckPublicApi() => CheckPublicApi ?? _releaseConfig?.CheckPublicApi ?? true;

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
