// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using Buildvana.Tool.CommandLine;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// The bv-global options, parsed from the command line by <see cref="CliArgSplitter"/> before the subcommand is
/// dispatched. Carries both the parsed values (consumed by services such as <c>GitService</c> and
/// <c>DotNetService</c>) and the <see cref="BvOptionAttribute"/>/<see cref="DescriptionAttribute"/> help metadata
/// reflected by the help renderer.
/// </summary>
/// <param name="Verbosity">The raw <c>--verbosity</c> / <c>-v</c> value, or <see langword="null"/> if none was passed.</param>
/// <param name="MainBranch">The raw <c>--main-branch</c> value, or <see langword="null"/> if none was passed.</param>
/// <param name="Color">Whether <c>--color</c> was passed.</param>
/// <param name="NoColor">Whether <c>--no-color</c> was passed.</param>
/// <param name="Nologo">Whether <c>--nologo</c> was passed.</param>
/// <param name="Version">Whether <c>--version</c> was passed.</param>
/// <remarks>The constructor parameter order is also the order in which these options appear in <c>bv</c>'s help.</remarks>
internal sealed record GlobalSettings(
    [property: BvOption("-v|--verbosity <LEVEL>")]
    [property: Description("Logging verbosity. One of: quiet, minimal, normal, detailed, diagnostic. Defaults to normal.")]
    string? Verbosity,
    [property: BvOption("--main-branch <NAME>")]
    [property: Description("Name of the repository's main branch. Defaults to 'main'.")]
    string? MainBranch,
    [property: BvOption("--color")]
    [property: Description("Force ANSI color output even when not connected to a TTY.")]
    bool Color,
    [property: BvOption("--no-color")]
    [property: Description("Disable ANSI color output.")]
    bool NoColor,
    [property: BvOption("--nologo")]
    [property: Description("Suppress the startup logo line.")]
    bool Nologo,
    [property: BvOption("--version")]
    [property: Description("Print the bv version and exit.")]
    bool Version);
