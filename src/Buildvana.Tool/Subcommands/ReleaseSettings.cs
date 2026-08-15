// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Core.Versioning;
using Buildvana.Tool.CommandLine;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>release</c> command, parsed from the command-line option tokens by <see cref="Parse"/>.
/// Decorated with <see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/> for the help renderer and
/// the argument validator. The configuration-overriding flags (<c>--configuration</c>, <c>--check-public-api</c>,
/// <c>--dogfood</c>) reach resolution through <c>CommandLineOverridesParser</c> and the configuration factory,
/// so commands read their effective values from the resolved <c>BuildvanaConfig</c>; only <see cref="Bump"/>,
/// which is not configuration, is consumed from here.
/// </summary>
internal sealed class ReleaseSettings
{
    /// <summary>
    /// Gets the MSBuild configuration to build.
    /// </summary>
    [BvOption("-c|--configuration <NAME>")]
    [Description("MSBuild configuration to build. Defaults to the configured value, or 'Release'.")]
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
    /// Gets a value indicating whether in-tree references to packages produced by this release are updated.
    /// </summary>
    [BvOption("--dogfood <BOOL>")]
    [Description("Update in-tree references to packages produced by this release. Defaults to true.")]
    public bool? Dogfood { get; init; }

    /// <summary>
    /// Parses the command's option tokens into a <see cref="ReleaseSettings"/>. Unknown options have already
    /// been rejected by <c>CommandArgumentValidator</c>, so every option token is one the command declares.
    /// </summary>
    /// <param name="options">The option tokens for the <c>release</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">An option value is invalid.</exception>
    public static ReleaseSettings Parse(IReadOnlyList<string> options)
    {
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        return new ReleaseSettings
        {
            Configuration = reader.ReadValue("--configuration", "-c"),
            Bump = reader.ReadValue("--bump"),
            CheckPublicApi = reader.ReadBoolValue("--check-public-api"),
            Dogfood = reader.ReadBoolValue("--dogfood"),
        };
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
}
