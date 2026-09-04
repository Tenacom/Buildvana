// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Buildvana.Core;
using Buildvana.Tool.CommandLine;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Enforces the argument contract for a dispatched command, and completes the split that
/// <see cref="CliArgSplitter"/> leaves unfinished.
/// </summary>
/// <remarks>
/// <para>A forwarding command takes no tokens before <c>--</c>: everything to forward goes after it. A
/// non-forwarding command has nowhere to forward, so it rejects anything after <c>--</c>. It accepts as many
/// positionals as its settings type declares through <see cref="BvArgumentAttribute"/>, and only the options that
/// type declares through <see cref="BvOptionAttribute"/>. A command with no settings type declares neither, so it
/// takes no options at all. A command whose last declared argument is variadic has no upper bound: that argument
/// takes every positional the ones before it left.</para>
/// <para>The splitter stops collecting positionals at the first option token, so an operand written after an
/// option arrives here among the option tokens. The declared options are read out of those tokens with their
/// arity, and every non-option token left over is a positional. <c>bv deps update --check Serilog</c> therefore
/// names the same pin as <c>bv deps update Serilog --check</c>. A leftover token that starts with <c>-</c> is an
/// option the command does not declare, and is an error. The settings type's <c>Parse</c> can assume that every
/// option token it receives is one the command declares.</para>
/// </remarks>
internal static class CommandArgumentValidator
{
    /// <summary>
    /// Validates the parsed command line against the dispatched command's argument rules, and binds its tokens.
    /// </summary>
    /// <param name="command">The command being dispatched.</param>
    /// <param name="parsed">The parsed command line.</param>
    /// <param name="positionals">The positional tokens left over after subcommand resolution.</param>
    /// <returns>The tokens the command receives: its options, its positionals, and the forwarded tokens.</returns>
    /// <exception cref="BuildFailedException">An argument is not valid for the command.</exception>
    public static CommandParameters Validate(
        CommandRegistration command,
        ParsedCommandLine parsed,
        IReadOnlyList<string> positionals)
    {
        Guard.IsNotNull(command);
        Guard.IsNotNull(parsed);
        Guard.IsNotNull(positionals);
        if (command.ConsumesAllArguments)
        {
            if (parsed.OptionTokens.Count > 0 || positionals.Count > 0)
            {
                var offending = parsed.OptionTokens.Count > 0 ? parsed.OptionTokens[0] : positionals[0];
                var message = $"Unexpected argument '{offending}' for command '{command.Name}'. "
                    + $"Forward arguments to dotnet after '--', e.g. 'bv {command.Name} -- {offending}'.";
                throw new BuildFailedException(ExitCodes.Usage, message);
            }

            return new CommandParameters(parsed.OptionTokens, positionals, parsed.Forwarded);
        }

        if (parsed.Forwarded.Count > 0)
        {
            throw new BuildFailedException(
                ExitCodes.Usage,
                $"Command '{command.Name}' does not forward arguments; remove the '--' separator and everything after it.");
        }

        var arguments = DeclaredArguments(command);
        var reader = new CliOptionReader(parsed.OptionTokens);
        ConsumeDeclaredOptions(reader, command);
        var strays = reader.Remaining;
        var bound = BindPositionals(command, arguments, positionals, strays);
        for (var i = bound.Count; i < arguments.Count; i++)
        {
            if (arguments[i].Required)
            {
                throw new BuildFailedException(
                    ExitCodes.Usage,
                    $"Missing required argument <{arguments[i].Name}> for command '{command.Name}'.");
            }
        }

        return new CommandParameters(WithoutStrays(parsed.OptionTokens, strays), bound, parsed.Forwarded);
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

    private static void ConsumeDeclaredOptions(CliOptionReader reader, CommandRegistration command)
    {
        foreach (var option in DeclaredOptions(command))
        {
            foreach (var name in option.LongNames.Concat(option.ShortNames))
            {
                if (option.ValueName is null)
                {
                    _ = reader.ReadFlag(name);
                }
                else
                {
                    _ = reader.ReadValue(name);
                }
            }
        }
    }

    private static IEnumerable<BvOptionAttribute> DeclaredOptions(CommandRegistration command)
    {
        if (command.SettingsType is null)
        {
            return [];
        }

        return command.SettingsType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static p => p.GetCustomAttribute<BvOptionAttribute>())
            .OfType<BvOptionAttribute>();
    }

    // A stray is a token no declared option consumed: either an operand written after an option, or an option
    // the command does not declare. Strays are examined in command-line order, and so is the positional the
    // subcommand walk left, which precedes every stray. A botched command line therefore names its first
    // offending token, whichever of the two kinds it is: `bv clean junk --bogus` names `junk`, and
    // `bv clean --bogus junk` names `--bogus`.
    private static List<string> BindPositionals(
        CommandRegistration command,
        IReadOnlyList<BvArgumentAttribute> arguments,
        IReadOnlyList<string> positionals,
        IReadOnlyList<string> strays)
    {
        var bounded = arguments is not [.., { Variadic: true }];
        if (bounded && positionals.Count > arguments.Count)
        {
            throw UnexpectedArgument(positionals[arguments.Count], command);
        }

        var bound = new List<string>(positionals);
        foreach (var stray in strays)
        {
            if (CliArgSplitter.IsOption(stray))
            {
                throw new BuildFailedException(
                    ExitCodes.Usage,
                    $"Unknown option '{stray}' for command '{command.Name}'.");
            }

            if (bounded && bound.Count == arguments.Count)
            {
                throw UnexpectedArgument(stray, command);
            }

            bound.Add(stray);
        }

        return bound;
    }

    private static BuildFailedException UnexpectedArgument(string token, CommandRegistration command)
        => new(ExitCodes.Usage, $"Unexpected argument '{token}' for command '{command.Name}'.");

    // The option tokens the command receives: the given tokens minus the strays, which by this point are all
    // operands. The reader works on a copy of the token list and removes what it consumes, so the strays are a
    // subsequence of that list and one ordered pass removes them.
    private static IReadOnlyList<string> WithoutStrays(IReadOnlyList<string> tokens, IReadOnlyList<string> strays)
    {
        if (strays.Count == 0)
        {
            return tokens;
        }

        var result = new List<string>(tokens.Count - strays.Count);
        var strayIndex = 0;
        foreach (var token in tokens)
        {
            if (strayIndex < strays.Count && string.Equals(token, strays[strayIndex], StringComparison.Ordinal))
            {
                strayIndex++;
                continue;
            }

            result.Add(token);
        }

        return result;
    }
}
