// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
/// <param name="Color">Whether <c>--color</c> was passed.</param>
/// <param name="NoColor">Whether <c>--no-color</c> was passed.</param>
/// <param name="Nologo">Whether <c>--nologo</c> was passed.</param>
/// <param name="SkipSdkCheck">Whether <c>--skip-sdk-check</c> was passed.</param>
/// <param name="SkipDelegation">Whether <c>--skip-delegation</c> was passed.</param>
/// <param name="Version">Whether <c>--version</c> was passed.</param>
/// <remarks>The constructor parameter order is also the order in which these options appear in <c>bv</c>'s help.</remarks>
internal sealed record GlobalSettings(
    [property: BvOption("-v|--verbosity <LEVEL>")]
    [property: Description("Logging verbosity. One of: quiet, minimal, normal, detailed, diagnostic. Defaults to minimal.")]
    string? Verbosity,
    [property: BvOption("--color")]
    [property: Description("Force ANSI color output even when not connected to a TTY.")]
    bool Color,
    [property: BvOption("--no-color")]
    [property: Description("Disable ANSI color output.")]
    bool NoColor,
    [property: BvOption("--nologo")]
    [property: Description("Suppress the startup logo line.")]
    bool Nologo,
    [property: BvOption("--skip-sdk-check")]
    [property: Description("Skip the Buildvana SDK version check performed by commands that use the SDK.")]
    bool SkipSdkCheck,
    [property: BvOption("--skip-delegation")]
    [property: Description("Run this exact bv instead of delegating to the version pinned in the repository's tool manifest.")]
    bool SkipDelegation,
    [property: BvOption("--version")]
    [property: Description("Print the bv version and exit.")]
    bool Version)
{
    /// <summary>
    /// Renders the options back into command-line tokens, for a <c>bv</c> run spawned on behalf of this one.
    /// <see cref="Version"/> is left out: it ends a run instead of configuring one.
    /// </summary>
    /// <returns>The tokens, in the order of the options in <c>bv</c>'s help; empty when no option was passed.</returns>
    public IReadOnlyList<string> ToTokens()
    {
        var tokens = new List<string>();
        if (Verbosity is not null)
        {
            tokens.Add("--verbosity");
            tokens.Add(Verbosity);
        }

        AddFlag(Color, "--color");
        AddFlag(NoColor, "--no-color");
        AddFlag(Nologo, "--nologo");
        AddFlag(SkipSdkCheck, "--skip-sdk-check");
        AddFlag(SkipDelegation, "--skip-delegation");
        return tokens;

        void AddFlag(bool present, string token)
        {
            if (present)
            {
                tokens.Add(token);
            }
        }
    }
}
