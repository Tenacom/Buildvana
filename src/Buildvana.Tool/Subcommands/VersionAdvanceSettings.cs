// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Core.Versioning;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>version advance</c> command, parsed from the command's positional and option tokens by
/// <see cref="Parse"/>.
/// Decorated with <see cref="BvArgumentAttribute"/>/<see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/>
/// for the help renderer and the argument validator. The <c>--check-public-api</c> flag reaches resolution
/// through <c>CommandLineOverridesParser</c> and the configuration factory, so the command reads its effective
/// value from the resolved <c>BuildvanaConfig</c>.
/// </summary>
internal sealed class VersionAdvanceSettings
{
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
    /// Parses the command's positional and option tokens into a <see cref="VersionAdvanceSettings"/>. Excess
    /// positionals and unknown options have already been rejected by <c>CommandArgumentValidator</c>, so at
    /// most one positional is present and every option token is one the command declares.
    /// </summary>
    /// <param name="positionals">The positional tokens for the <c>version advance</c> command
    /// (from <c>CommandParameters.Positionals</c>).</param>
    /// <param name="options">The option tokens for the <c>version advance</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">An option value is invalid.</exception>
    public static VersionAdvanceSettings Parse(IReadOnlyList<string> positionals, IReadOnlyList<string> options)
    {
        Guard.IsNotNull(positionals);
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        return new VersionAdvanceSettings
        {
            Change = positionals.Count > 0 ? positionals[0] : null,
            CheckPublicApi = reader.ReadBoolValue("--check-public-api"),
            Force = reader.ReadFlag("--force"),
        };
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
            : throw new BuildFailedException(
                ExitCodes.Usage,
                $"Invalid value '{Change}' for CHANGE. Valid values: none, unstable, stable, minor, major.");
    }
}
