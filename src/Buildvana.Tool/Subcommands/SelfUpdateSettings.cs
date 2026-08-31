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
    /// Gets the version to stamp instead of this bv's own, or <see langword="null"/> to stamp this bv's own version.
    /// </summary>
    [BvOption("--to <VERSION>")]
    [Description("Version to stamp into the repository's pins. Defaults to this bv's own version.")]
    public string? To { get; init; }

    /// <summary>
    /// Parses the command's option tokens into a <see cref="SelfUpdateSettings"/>. Unknown options have already
    /// been rejected by <c>CommandArgumentValidator</c>, so every option token is one the command declares.
    /// </summary>
    /// <param name="options">The option tokens for the <c>self-update</c> command (from <c>CommandParameters.Options</c>).</param>
    /// <returns>The parsed settings.</returns>
    public static SelfUpdateSettings Parse(IReadOnlyList<string> options)
    {
        Guard.IsNotNull(options);
        var reader = new CliOptionReader(options);
        return new SelfUpdateSettings
        {
            Force = reader.ReadFlag("--force"),
            To = reader.ReadValue("--to"),
        };
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
}
