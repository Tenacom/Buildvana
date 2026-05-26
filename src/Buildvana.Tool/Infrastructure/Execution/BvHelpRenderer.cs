// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Commands;
using CommunityToolkit.Diagnostics;
using Spectre.Console;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Renders <c>bv</c>'s help pages using <c>Spectre.Console</c> primitives. Global options come from reflecting
/// <see cref="GlobalSettings"/>; per-command options from the command's settings type; the command list and
/// forwarding annotations from <see cref="CommandRegistry"/>. Option metadata is read from
/// <see cref="BvOptionAttribute"/> and <see cref="DescriptionAttribute"/>.
/// </summary>
internal sealed class BvHelpRenderer(IAnsiConsole console)
{
    /// <summary>
    /// Writes the root help page (usage, global options, and the command list).
    /// </summary>
    public void WriteRootHelp()
    {
        WriteUsage("[OPTIONS] <COMMAND>");
        WriteGlobalOptions();
        WriteCommands();
    }

    /// <summary>
    /// Writes the help page for a single command.
    /// </summary>
    /// <param name="command">The command to describe.</param>
    public void WriteCommandHelp(CommandRegistration command)
    {
        Guard.IsNotNull(command);
        if (command.ConsumesAllArguments)
        {
            WriteUsage($"{command.Name} [-- <ARGS FORWARDED TO DOTNET>]");
            WriteGlobalOptions();
            WriteForwardedArguments();
        }
        else
        {
            WriteUsage($"{command.Name} [OPTIONS]");
            if (command.SettingsType is not null)
            {
                WriteOptionGrid("OPTIONS", EnumerateOptions(command.SettingsType));
            }

            WriteGlobalOptions();
        }
    }

    private static Grid NewGrid()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn { Padding = new Padding(4, 4), NoWrap = true });
        grid.AddColumn(new GridColumn { Padding = new Padding(0, 0) });
        return grid;
    }

    private static IEnumerable<(string Names, string? Description)> EnumerateOptions(Type settingsType)
    {
        foreach (var property in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var option = property.GetCustomAttribute<BvOptionAttribute>();
            if (option is null)
            {
                continue;
            }

            var description = property.GetCustomAttribute<DescriptionAttribute>();
            yield return (FormatNames(option), description?.Description);
        }
    }

    private static string FormatNames(BvOptionAttribute option)
    {
        // Pad the short-name slot when an option has none, so long names align across rows.
        var shortPart = option.ShortNames.Count > 0 ? option.ShortNames[0] + ", " : "    ";
        var longPart = string.Join(", ", option.LongNames);
        var valuePart = option.ValueName is null ? string.Empty : $" <{option.ValueName}>";
        return shortPart + longPart + valuePart;
    }

    private static string GetDescription(Type type) => type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;

    private static string StripTrailingPeriod(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.EndsWith('.') ? text[..^1] : text;
    }

    private void WriteUsage(string tail)
    {
        console.Markup("\nUSAGE:\n");
        console.MarkupInterpolated($"    bv {tail}\n");
    }

    private void WriteGlobalOptions()
    {
        var helpRow = (FormatNames(new BvOptionAttribute("-h|--help")), (string?)"Prints help information");
        WriteOptionGrid("GLOBAL OPTIONS", EnumerateOptions(typeof(GlobalSettings)).Append(helpRow));
    }

    private void WriteCommands()
    {
        console.Markup("\nCOMMANDS:\n");
        var grid = NewGrid();
        foreach (var command in CommandRegistry.Commands)
        {
            var description = Markup.Escape(StripTrailingPeriod(GetDescription(command.CommandType)));
            var rendered = command.ConsumesAllArguments
                ? $"{description}   [grey][[forwards extra args to dotnet]][/]"
                : description;
            grid.AddRow(new Markup(Markup.Escape(command.Name)), new Markup(rendered));
        }

        console.Write(grid);
    }

    private void WriteForwardedArguments()
    {
        console.Markup("\nFORWARDED ARGUMENTS:\n");
        console.Markup("    Any arguments after the [grey]--[/] separator are forwarded verbatim to the dotnet invocation(s) this command performs.\n");
    }

    private void WriteOptionGrid(string header, IEnumerable<(string Names, string? Description)> options)
    {
        console.Markup($"\n{header}:\n");
        var grid = NewGrid();
        foreach (var (names, description) in options)
        {
            grid.AddRow(new Markup(Markup.Escape(names)), new Markup(StripTrailingPeriod(description)));
        }

        console.Write(grid);
    }
}
