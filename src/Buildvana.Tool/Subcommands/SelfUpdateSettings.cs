// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Options for the <c>self-update</c> command, parsed from the command's option tokens by <see cref="Parse"/>.
/// Decorated with <see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/> for the help renderer.
/// </summary>
internal sealed class SelfUpdateSettings
{
    /// <summary>
    /// Gets a value indicating whether the update may downgrade pins that are newer than the target version.
    /// </summary>
    [BvOption("--force")]
    [Description("Update the repository's pins even when they are newer than the target version (downgrade).")]
    public bool Force { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target is the latest version the package sources list, prereleases
    /// included.
    /// </summary>
    [BvOption("--preview")]
    [Description("Update to the latest version on the package sources, prereleases included.")]
    public bool Preview { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target is the version the tool manifest pins, with no package source asked.
    /// </summary>
    [BvOption("--repair")]
    [Description("Update the repository to the bv version the tool manifest pins, without asking the package sources.")]
    public bool Repair { get; init; }

    /// <summary>
    /// Gets the version to stamp, or <see langword="null"/> when the command line names none.
    /// </summary>
    [BvOption("--to <VERSION>")]
    [Description("Update the repository to this version, without asking the package sources.")]
    public string? To { get; init; }

    /// <summary>
    /// Parses the command's option tokens into a <see cref="SelfUpdateSettings"/>. Unknown options have already
    /// been rejected by <c>CommandArgumentValidator</c>, so every option token is one the command declares.
    /// </summary>
    /// <param name="options">The option tokens for the <c>self-update</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    /// <exception cref="BuildFailedException">More than one of <c>--preview</c>, <c>--repair</c>, and <c>--to</c>
    /// was given.</exception>
    public static SelfUpdateSettings Parse(IReadOnlyList<string> options)
    {
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        var settings = new SelfUpdateSettings
        {
            Force = reader.ReadFlag("--force"),
            Preview = reader.ReadFlag("--preview"),
            Repair = reader.ReadFlag("--repair"),
            To = reader.ReadValue("--to"),
        };

        Validate(settings);
        return settings;
    }

    /// <summary>
    /// Parses <see cref="To"/> into a <see cref="NuGetVersion"/>; <see langword="null"/> when the option was
    /// not given.
    /// </summary>
    /// <returns>The parsed version, or <see langword="null"/>.</returns>
    /// <exception cref="BuildFailedException">The value of <see cref="To"/> is not a valid version.</exception>
    public NuGetVersion? ResolveTo()
    {
        if (To is null)
        {
            return null;
        }

        return NuGetVersion.TryParse(To, out var version)
            ? version
            : throw new BuildFailedException(
                ExitCodes.Usage,
                $"Invalid value '{To}' for --to. Expected a version, e.g. 2.1.0 or 2.1.0-preview.");
    }

    // Each of the three names the target version, and no two answers agree.
    private static void Validate(SelfUpdateSettings settings)
    {
        var given = new List<string>(3);
        if (settings.Preview)
        {
            given.Add("--preview");
        }

        if (settings.Repair)
        {
            given.Add("--repair");
        }

        if (settings.To is not null)
        {
            given.Add("--to");
        }

        if (given.Count < 2)
        {
            return;
        }

        var names = given.Count == 2 ? $"{given[0]} and {given[1]}" : $"{given[0]}, {given[1]}, and {given[2]}";
        throw new BuildFailedException(ExitCodes.Usage, $"{names} each name the target version, so they do not go together.");
    }
}
