// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Buildvana.Tool.Subcommands;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// The structured result of splitting a raw <c>bv</c> command line: the parsed globals, the subcommand and its
/// positional parameters, the option tokens left for the command's <c>*Settings</c> to parse, and the tokens
/// forwarded verbatim after the <c>--</c> separator.
/// </summary>
/// <param name="Globals">The globals parsed (and stripped) from the tokens before <c>--</c>.</param>
/// <param name="HelpRequested">Whether <c>--help</c>/<c>-h</c> appeared before <c>--</c>.</param>
/// <param name="Subcommand">The subcommand (first non-option token before <c>--</c>), or <see langword="null"/> if none.</param>
/// <param name="Positionals">The non-option tokens immediately following the subcommand, in order.</param>
/// <param name="OptionTokens">The remaining non-global, non-positional tokens before <c>--</c>, for the command to parse.</param>
/// <param name="Forwarded">The tokens after the first <c>--</c>, to forward verbatim.</param>
internal sealed record ParsedCommandLine(
    GlobalSettings Globals,
    bool HelpRequested,
    string? Subcommand,
    IReadOnlyList<string> Positionals,
    IReadOnlyList<string> OptionTokens,
    IReadOnlyList<string> Forwarded);
