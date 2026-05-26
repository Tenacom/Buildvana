// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Buildvana.Core;
using Buildvana.Tool.Commands;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// Splits a raw <c>bv</c> command line into its parts (see <see cref="ParsedCommandLine"/>). The pipeline is:
/// find the first <c>--</c> (everything after it is forwarded verbatim); strip globals and <c>--help</c>/<c>-h</c>
/// from the tokens before it; then classify the residue — the first non-option token is the subcommand, the
/// non-option tokens immediately following it are positionals, and everything else is left for the command to parse.
/// </summary>
internal static class CliArgSplitter
{
    /// <summary>
    /// Splits the given raw arguments.
    /// </summary>
    /// <param name="args">The raw command-line arguments, as received by <c>Main</c>.</param>
    /// <returns>The structured <see cref="ParsedCommandLine"/>.</returns>
    /// <exception cref="BuildFailedException">A value-bearing global was given with no following value.</exception>
    public static ParsedCommandLine Split(IReadOnlyList<string> args)
    {
        Guard.IsNotNull(args);

        var (working, forwarded) = SplitOnSeparator(args);

        var reader = new CliOptionReader(working);
        var verbosity = reader.ReadValue("--verbosity", "-v");
        var mainBranch = reader.ReadValue("--main-branch");
        var color = reader.ReadFlag("--color");
        var noColor = reader.ReadFlag("--no-color");
        var nologo = reader.ReadFlag("--nologo");
        var version = reader.ReadFlag("--version");
        var helpRequested = reader.ReadFlag("--help", "-h");
        var globals = new GlobalOptions(verbosity, color, noColor, nologo, version, mainBranch);

        var (subcommand, positionals, optionTokens) = Classify(reader.Remaining);
        return new ParsedCommandLine(globals, helpRequested, subcommand, positionals, optionTokens, forwarded);
    }

    private static (IReadOnlyList<string> Working, IReadOnlyList<string> Forwarded) SplitOnSeparator(IReadOnlyList<string> args)
    {
        var separatorIndex = -1;
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], "--", StringComparison.Ordinal))
            {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex < 0)
        {
            return ([..args], []);
        }

        var working = new List<string>(separatorIndex);
        for (var i = 0; i < separatorIndex; i++)
        {
            working.Add(args[i]);
        }

        var forwarded = new List<string>(args.Count - separatorIndex - 1);
        for (var i = separatorIndex + 1; i < args.Count; i++)
        {
            forwarded.Add(args[i]);
        }

        return (working, forwarded);
    }

    private static (string? Subcommand, IReadOnlyList<string> Positionals, IReadOnlyList<string> OptionTokens) Classify(IReadOnlyList<string> residue)
    {
        var subcommandIndex = -1;
        for (var i = 0; i < residue.Count; i++)
        {
            if (!IsOption(residue[i]))
            {
                subcommandIndex = i;
                break;
            }
        }

        if (subcommandIndex < 0)
        {
            return (null, [], [..residue]);
        }

        var positionalEnd = subcommandIndex + 1;
        while (positionalEnd < residue.Count && !IsOption(residue[positionalEnd]))
        {
            positionalEnd++;
        }

        var positionals = new List<string>(positionalEnd - subcommandIndex - 1);
        for (var i = subcommandIndex + 1; i < positionalEnd; i++)
        {
            positionals.Add(residue[i]);
        }

        var optionTokens = new List<string>();
        for (var i = 0; i < subcommandIndex; i++)
        {
            optionTokens.Add(residue[i]);
        }

        for (var i = positionalEnd; i < residue.Count; i++)
        {
            optionTokens.Add(residue[i]);
        }

        return (residue[subcommandIndex], positionals, optionTokens);
    }

    private static bool IsOption(string token) => token.StartsWith('-');
}
