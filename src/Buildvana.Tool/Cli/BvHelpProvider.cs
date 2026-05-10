// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Custom help provider for the <c>bv</c> CLI tool. Extends Spectre's <see cref="HelpProvider"/> with:
/// a <c>GLOBAL OPTIONS</c> section at root help (reflecting <see cref="BaseSettings"/>);
/// an <c>[MSBuild: ...]</c> annotation on each command row of the root commands list (driven by
/// <see cref="AcceptsMSBuildOptionsAttribute"/>); and a <c>FORWARDED MSBUILD OPTIONS</c> section on
/// per-command help describing what kinds of <c>/</c>-prefixed switches the command forwards.
/// </summary>
internal sealed class BvHelpProvider(ICommandAppSettings settings) : HelpProvider(settings)
{
    private static readonly Dictionary<string, MSBuildOptionKinds> CommandKindsByName = BuildCommandKindsMap();

    public override IEnumerable<IRenderable> GetOptions(ICommandModel model, ICommandInfo? command)
    {
        if (command is not null)
        {
            foreach (var renderable in base.GetOptions(model, command))
            {
                yield return renderable;
            }

            foreach (var renderable in RenderForwardedMSBuildOptionsSection(command))
            {
                yield return renderable;
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
            var kind = CommandKindsByName.GetValueOrDefault(child.Name, MSBuildOptionKinds.None);
            var indicator = FormatKindIndicator(kind);
            var rendered = indicator is null
                ? description
                : $"{description}   [grey][[MSBuild: {indicator}]][/]";

            grid.AddRow(new Markup(Markup.Escape(child.Name)), new Markup(rendered));
        }

        yield return grid;
    }

    private static IEnumerable<IRenderable> RenderForwardedMSBuildOptionsSection(ICommandInfo command)
    {
        var kind = CommandKindsByName.GetValueOrDefault(command.Name, MSBuildOptionKinds.None);
        var text = kind switch
        {
            MSBuildOptionKinds.None => "MSBuild not invoked. No /-prefixed switches accepted.",
            MSBuildOptionKinds.Properties => "Accepts /p:Key=Value (or -p:Key=Value) MSBuild properties.",
            MSBuildOptionKinds.Switches => "Accepts /-prefixed MSBuild switches other than properties (e.g. /m:N, /v:m).",
            MSBuildOptionKinds.All => "Accepts all /-prefixed MSBuild switches: /p:Key=Value properties and others (e.g. /m:N, /v:m).",
            _ => throw new InvalidOperationException($"Unexpected {nameof(MSBuildOptionKinds)} value: {kind}"),
        };

        yield return new Markup("\nFORWARDED MSBUILD OPTIONS:\n");
        yield return new Markup($"    {Markup.Escape(text)}\n");
    }

    private static string? FormatKindIndicator(MSBuildOptionKinds kind) => kind switch
    {
        MSBuildOptionKinds.None => null,
        MSBuildOptionKinds.Properties => "properties only",
        MSBuildOptionKinds.Switches => "switches only",
        MSBuildOptionKinds.All => "all",
        _ => throw new InvalidOperationException($"Unexpected {nameof(MSBuildOptionKinds)} value: {kind}"),
    };

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

    private static Dictionary<string, MSBuildOptionKinds> BuildCommandKindsMap()
    {
        var result = new Dictionary<string, MSBuildOptionKinds>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(BvHelpProvider).Assembly.GetTypes())
        {
            if (type.Namespace is null || (type.Namespace != "Buildvana.Tool.Cli" && !type.Namespace.StartsWith("Buildvana.Tool.Cli.", StringComparison.Ordinal)))
            {
                continue;
            }

            if (!type.Name.EndsWith("Command", StringComparison.Ordinal))
            {
                continue;
            }

            var attr = type.GetCustomAttribute<AcceptsMSBuildOptionsAttribute>();
            if (attr is null)
            {
                continue;
            }

            var name = type.Name[..^"Command".Length];
            result[name] = attr.Kinds;
        }

        return result;
    }
}
