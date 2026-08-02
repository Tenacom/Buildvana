// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Subcommands;
using CommunityToolkit.Diagnostics;
using Spectre.Console;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Renders <c>bv</c>'s help pages using <c>Spectre.Console</c> primitives. Global options come from reflecting
/// <see cref="GlobalSettings"/>; per-command options and arguments from the command's settings type; the command
/// tree, node descriptions, and forwarding annotations from <see cref="CommandRegistry"/>. Option and argument
/// metadata is read from <see cref="BvOptionAttribute"/>, <see cref="BvArgumentAttribute"/>, and
/// <see cref="DescriptionAttribute"/>.
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
    /// Writes the help page for a command tree node: a subcommand listing for a node with children,
    /// a single command's help page otherwise.
    /// </summary>
    /// <param name="node">The node to describe.</param>
    public void WriteNodeHelp(CommandNode node)
    {
        Guard.IsNotNull(node);
        if (node.HasChildren)
        {
            WriteGroupHelp(node);
        }
        else
        {
            WriteCommandHelp(node);
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

    private static List<(string Template, string? Description)> EnumerateArguments(Type? settingsType)
    {
        if (settingsType is null)
        {
            return [];
        }

        var arguments = new List<(string Template, string? Description)>();
        foreach (var property in settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var argument = property.GetCustomAttribute<BvArgumentAttribute>();
            if (argument is null)
            {
                continue;
            }

            var description = property.GetCustomAttribute<DescriptionAttribute>();
            arguments.Add((argument.Template, description?.Description));
        }

        return arguments;
    }

    private static string FormatNames(BvOptionAttribute option)
    {
        // Pad the short-name slot when an option has none, so long names align across rows.
        var shortPart = option.ShortNames.Count > 0 ? option.ShortNames[0] + ", " : "    ";
        var longPart = string.Join(", ", option.LongNames);
        var valuePart = option.ValueName is null ? string.Empty : $" <{option.ValueName}>";
        return shortPart + longPart + valuePart;
    }

    private static string StripTrailingPeriod(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.EndsWith('.') ? text[..^1] : text;
    }

    private void WriteGroupHelp(CommandNode node)
    {
        // When the node is also a command (one aliased onto the group's path), the subcommand is optional:
        // invoking the group bare runs that command.
        WriteUsage(node.Command is null ? $"{node.FullName} <SUBCOMMAND>" : $"{node.FullName} [SUBCOMMAND]");
        WriteSubcommands(node);
        if (node.Command?.SettingsType is { } settingsType)
        {
            var options = EnumerateOptions(settingsType).ToList();
            if (options.Count > 0)
            {
                WriteOptionGrid("OPTIONS", options);
            }
        }

        WriteGlobalOptions();
    }

    private void WriteCommandHelp(CommandNode node)
    {
        var command = node.Command;
        Guard.IsNotNull(command);
        if (command.ConsumesAllArguments)
        {
            WriteUsage($"{node.FullName} [-- <ARGS FORWARDED TO DOTNET>]");
            WriteGlobalOptions();
            WriteForwardedArguments();
        }
        else
        {
            var arguments = EnumerateArguments(command.SettingsType);
            var usageParts = new List<string> { node.FullName };
            usageParts.AddRange(arguments.Select(static a => a.Template));
            usageParts.Add("[OPTIONS]");
            WriteUsage(string.Join(' ', usageParts));
            if (arguments.Count > 0)
            {
                WriteOptionGrid("ARGUMENTS", arguments);
            }

            if (command.SettingsType is not null)
            {
                var options = EnumerateOptions(command.SettingsType).ToList();
                if (options.Count > 0)
                {
                    WriteOptionGrid("OPTIONS", options);
                }
            }

            WriteGlobalOptions();
        }
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
        foreach (var node in CommandRegistry.TopLevelNodes)
        {
            var description = Markup.Escape(StripTrailingPeriod(node.Description));
            var rendered = node.Command is { ConsumesAllArguments: true }
                ? $"{description}   [grey][[forwards extra args to dotnet]][/]"
                : description;
            grid.AddRow(new Markup(Markup.Escape(node.Name)), new Markup(rendered));
        }

        console.Write(grid);
    }

    private void WriteSubcommands(CommandNode node)
    {
        console.Markup("\nSUBCOMMANDS:\n");
        var grid = NewGrid();
        foreach (var child in node.Children)
        {
            var description = Markup.Escape(StripTrailingPeriod(child.Description));
            if (node.Command is not null && ReferenceEquals(child.Command, node.Command))
            {
                description += "   [grey][[default]][/]";
            }

            grid.AddRow(new Markup(Markup.Escape(child.Name)), new Markup(description));
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
            // Description is not escaped because it may contain markup (e.g., `[bold]Required[/]`).
            grid.AddRow(new Markup(Markup.Escape(names)), new Markup(StripTrailingPeriod(description)));
        }

        console.Write(grid);
    }
}
