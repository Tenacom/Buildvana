// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Enforces the argument contract for a dispatched command:
/// forwarding commands take no tokens before <c>--</c> (everything to forward goes after it);
/// non-forwarding commands have nowhere to forward, so they reject anything after <c>--</c>, and accept only as
/// many positionals as their settings type declares via <see cref="BvArgumentAttribute"/>.
/// Option tokens are left to the command's settings type to parse (and reject leftovers of); a command with no
/// settings type takes no options at all, so any option token is rejected here.
/// </summary>
internal static class CommandArgumentValidator
{
    /// <summary>
    /// Validates the parsed command line against the dispatched command's argument rules.
    /// </summary>
    /// <param name="command">The command being dispatched.</param>
    /// <param name="parsed">The parsed command line.</param>
    /// <param name="positionals">The positional tokens left over after subcommand resolution.</param>
    /// <exception cref="BuildFailedException">An argument is not valid for the command.</exception>
    public static void Validate(CommandRegistration command, ParsedCommandLine parsed, IReadOnlyList<string> positionals)
    {
        Guard.IsNotNull(command);
        Guard.IsNotNull(parsed);
        Guard.IsNotNull(positionals);
        if (command.ConsumesAllArguments)
        {
            if (parsed.OptionTokens.Count > 0 || positionals.Count > 0)
            {
                var offending = parsed.OptionTokens.Count > 0 ? parsed.OptionTokens[0] : positionals[0];
                throw new BuildFailedException(
                    $"Unexpected argument '{offending}' for command '{command.Name}'. Forward arguments to dotnet after '--', e.g. 'bv {command.Name} -- {offending}'.");
            }
        }
        else
        {
            if (parsed.Forwarded.Count > 0)
            {
                throw new BuildFailedException($"Command '{command.Name}' does not forward arguments; remove the '--' separator and everything after it.");
            }

            // Excess positionals are checked before unknown options so that in the typical shape of a botched
            // command line (`bv clean junk --bogus`) the offending tokens are reported in command-line order.
            var arguments = DeclaredArguments(command);
            if (positionals.Count > arguments.Count)
            {
                throw new BuildFailedException($"Unexpected argument '{positionals[arguments.Count]}' for command '{command.Name}'.");
            }

            if (command.SettingsType is null && parsed.OptionTokens.Count > 0)
            {
                throw new BuildFailedException($"Unknown option '{parsed.OptionTokens[0]}' for command '{command.Name}'.");
            }

            for (var i = positionals.Count; i < arguments.Count; i++)
            {
                if (arguments[i].Required)
                {
                    throw new BuildFailedException($"Missing required argument <{arguments[i].Name}> for command '{command.Name}'.");
                }
            }
        }
    }

    private static IReadOnlyList<BvArgumentAttribute> DeclaredArguments(CommandRegistration command)
    {
        if (command.SettingsType is null)
        {
            return [];
        }

        return [..command.SettingsType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static p => p.GetCustomAttribute<BvArgumentAttribute>())
            .OfType<BvArgumentAttribute>()];
    }
}
