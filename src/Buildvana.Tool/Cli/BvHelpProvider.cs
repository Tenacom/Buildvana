// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Buildvana.Tool.Commands;
using Buildvana.Tool.Infrastructure.Execution;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Custom help provider for the <c>bv</c> CLI tool. Extends Spectre's <see cref="HelpProvider"/> with:
/// a <c>GLOBAL OPTIONS</c> section at root help (reflecting <see cref="BaseSettings"/>); an annotation on
/// each command row of the root commands list marking the commands that forward extra arguments; and a
/// <c>FORWARDED ARGUMENTS</c> section on the per-command help of commands that consume all of their arguments
/// (see <see cref="CommandRegistry"/>).
/// </summary>
internal sealed class BvHelpProvider(ICommandAppSettings settings) : HelpProvider(settings)
{
    public override IEnumerable<IRenderable> GetOptions(ICommandModel model, ICommandInfo? command)
    {
        if (command is not null)
        {
            foreach (var renderable in base.GetOptions(model, command))
            {
                yield return renderable;
            }

            if (CommandRegistry.ConsumesAllArguments(command.Name))
            {
                yield return new Markup("\nFORWARDED ARGUMENTS:\n");
                yield return new Markup("    Any arguments other than the options above are forwarded verbatim to the dotnet invocation(s) this command performs.\n");
            }

            yield break;
        }

        // Root help (command is null): we deliberately skip base.GetOptions and render our own
        // GLOBAL OPTIONS grid. base would emit a separate OPTIONS: block containing only
        // -h, --help, which would duplicate/conflict with this section. If Spectre starts
        // producing additional root-level entries, fold them into EnumerateGlobalOptions
        // rather than re-enabling base — the hand-appended -h, --help row below exists
        // precisely because base is bypassed.
        yield return new Markup("\nGLOBAL OPTIONS:\n");

        var grid = new Grid();
        grid.AddColumn(new GridColumn { Padding = new Padding(4, 4), NoWrap = true });
        grid.AddColumn(new GridColumn { Padding = new Padding(0, 0) });

        foreach (var (names, description) in EnumerateGlobalOptions())
        {
            grid.AddRow(
                new Markup(Markup.Escape(names)),
                new Markup(Markup.Escape(StripTrailingPeriod(description))));
        }

        // Append Spectre's built-in --help so the section is self-contained.
        grid.AddRow(
            new Markup(Markup.Escape(FormatOptionNames(new CommandOptionAttribute("-h|--help")))),
            new Markup("Prints help information"));

        yield return grid;
    }

    public override IEnumerable<IRenderable> GetCommands(ICommandModel model, ICommandInfo? command)
    {
        var container = command ?? (ICommandContainer)model;
        var isDefaultCommand = command?.IsDefaultCommand ?? false;
        var commands = (isDefaultCommand ? model.Commands : container.Commands)
            .Where(static x => !x.IsHidden)
            .ToList();
        if (commands.Count == 0)
        {
            yield break;
        }

        yield return new Markup("\nCOMMANDS:\n");

        var grid = new Grid();
        grid.AddColumn(new GridColumn { Padding = new Padding(4, 4), NoWrap = true });
        grid.AddColumn(new GridColumn { Padding = new Padding(0, 0) });

        foreach (var child in commands)
        {
            var description = Markup.Escape(StripTrailingPeriod(child.Description));
            var rendered = CommandRegistry.ConsumesAllArguments(child.Name)
                ? $"{description}   [grey][[forwards extra args to dotnet]][/]"
                : description;

            grid.AddRow(new Markup(Markup.Escape(child.Name)), new Markup(rendered));
        }

        yield return grid;
    }

    private static IEnumerable<(string Names, string? Description)> EnumerateGlobalOptions()
    {
        var properties = typeof(BaseSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var prop in properties)
        {
            var co = prop.GetCustomAttribute<CommandOptionAttribute>();
            if (co is null)
            {
                continue;
            }

            var desc = prop.GetCustomAttribute<DescriptionAttribute>();
            yield return (FormatOptionNames(co), desc?.Description);
        }
    }

    private static string FormatOptionNames(CommandOptionAttribute co)
    {
        // Mirror Spectre's per-command OPTIONS layout, where the leading "-X, " slot is padded
        // when an option has no short name so that long names align across rows.
        var shortPart = co.ShortNames.Count > 0 ? $"-{co.ShortNames[0]}, " : "    ";
        var longPart = string.Join(", ", co.LongNames.Select(static n => "--" + n));
        var valuePart = co.ValueName is null ? string.Empty : $" <{co.ValueName}>";
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
}
