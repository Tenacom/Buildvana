// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Subcommands;

// The command pages, pinned against CommandRegistry. A test fails when a command or an option exists in the code
// and not on a page, or on a page and not in the code. An option is rendered as help renders it, short names
// first: `-c, --configuration <NAME>`. The repository root reaches the tests as ToolDiagnosticsPageTests reads it.
internal sealed class CommandPagesTests
{
    private static readonly string RepositoryRoot = typeof(CommandPagesTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "RepositoryRoot")
        .Value!;

    // The page under docs/tool-commands of each top-level command name. A group page holds one "Options" table
    // for all of its subcommands, with a "Subcommand" column.
    private static readonly Dictionary<string, string> Pages = new(StringComparer.Ordinal)
    {
        ["clean"] = "build-pipeline.md",
        ["restore"] = "build-pipeline.md",
        ["build"] = "build-pipeline.md",
        ["test"] = "build-pipeline.md",
        ["pack"] = "build-pipeline.md",
        ["dependencies"] = "dependencies.md",
        ["release"] = "release.md",
        ["self-update"] = "self-update.md",
        ["version"] = "version.md",
    };

    public static IEnumerable<string> CommandNames() => CommandRegistry.Commands.Select(static command => command.Name);

    [Test]
    public async Task CommandsTable_ListsTheCommandsOfCommandRegistry()
    {
        var page = LoadPage("command-line.md");
        var names = page.GetTableColumn("Commands", "Command");
        var aliases = page.GetTableColumn("Commands", "Aliases");
        var checks = page.GetTableColumn("Commands", "SDK version check");
        var listed = names.Select((name, index) => $"{name} | {aliases[index]} | {checks[index]}");
        var declared = CommandRegistry.Commands.Select(RenderCommand);

        await Assert.That(Join(listed)).IsEqualTo(Join(declared));
    }

    [Test]
    public async Task GlobalOptionsTable_ListsTheOptionsOfGlobalSettings()
    {
        var page = LoadPage("command-line.md");
        var listed = page.GetTableColumn("Global options", "Option");
        var declared = RenderOptions(typeof(GlobalSettings)).Append("-h, --help");

        await Assert.That(Join(listed)).IsEqualTo(Join(declared));
    }

    [Test]
    [MethodDataSource(nameof(CommandNames))]
    public async Task OptionsTable_ListsTheOptionsOfTheSettingsType(string name)
    {
        var command = CommandRegistry.Find(name)!;
        var page = LoadPage(Path.Combine("tool-commands", Pages[command.CanonicalPath[0]]));
        var listed = ListedOptions(page, command);
        IEnumerable<string> declared = command.SettingsType is null ? [] : RenderOptions(command.SettingsType);

        await Assert.That(Join(listed)).IsEqualTo(Join(declared));
    }

    private static DocumentationPage LoadPage(string relativePath)
        => DocumentationPage.Load(Path.Combine(RepositoryRoot, "docs", relativePath));

    // A row of the commands table: the canonical name, the aliases, and the SDK version check flag.
    private static string RenderCommand(CommandRegistration command)
    {
        var aliases = string.Join(", ", command.AliasPaths.Skip(1).Select(static path => string.Join(' ', path)));
        return $"{command.Name} | {aliases} | {(command.UsesSdk ? "yes" : "no")}";
    }

    private static IEnumerable<string> RenderOptions(Type settingsType)
        => settingsType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static property => property.GetCustomAttribute<BvOptionAttribute>())
            .Where(static option => option is not null)
            .Select(static option => RenderOption(option!));

    private static string RenderOption(BvOptionAttribute option)
    {
        var names = string.Join(", ", option.ShortNames.Take(1).Concat(option.LongNames));
        return option.ValueName is null ? names : $"{names} <{option.ValueName}>";
    }

    // The "Options" table of a page, or nothing when the page has no such section. On a group page, the rows
    // whose "Subcommand" cell names the subcommand.
    private static IEnumerable<string> ListedOptions(DocumentationPage page, CommandRegistration command)
    {
        if (!page.GetHeadings(2).Contains("Options"))
        {
            return [];
        }

        var options = page.GetTableColumn("Options", "Option");
        if (command.CanonicalPath is not [_, var subcommand])
        {
            return options;
        }

        var subcommands = page.GetTableColumn("Options", "Subcommand");
        return options.Where((_, index) => subcommands[index].Split(", ").Contains(subcommand));
    }

    private static string Join(IEnumerable<string> items) => string.Join("; ", items.Order(StringComparer.Ordinal));
}
