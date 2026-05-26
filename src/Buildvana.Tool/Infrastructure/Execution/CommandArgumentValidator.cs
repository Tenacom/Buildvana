// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Enforces the <c>--</c> forwarding contract for a dispatched command:
/// forwarding commands take no tokens before <c>--</c> (everything to forward goes after it);
/// non-forwarding commands have nowhere to forward, so they reject anything after <c>--</c> (and any positionals).
/// </summary>
internal static class CommandArgumentValidator
{
    /// <summary>
    /// Validates the parsed command line against the dispatched command's forwarding rules.
    /// </summary>
    /// <param name="command">The command being dispatched.</param>
    /// <param name="parsed">The parsed command line.</param>
    /// <exception cref="BuildFailedException">An argument is not valid for the command.</exception>
    public static void Validate(CommandRegistration command, ParsedCommandLine parsed)
    {
        Guard.IsNotNull(command);
        Guard.IsNotNull(parsed);
        if (command.ConsumesAllArguments)
        {
            if (parsed.OptionTokens.Count > 0 || parsed.Positionals.Count > 0)
            {
                var offending = parsed.OptionTokens.Count > 0 ? parsed.OptionTokens[0] : parsed.Positionals[0];
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

            if (parsed.Positionals.Count > 0)
            {
                throw new BuildFailedException($"Unexpected argument '{parsed.Positionals[0]}' for command '{command.Name}'.");
            }
        }
    }
}
