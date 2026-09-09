// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.RegularExpressions;
using Buildvana.Core.Testing;

// The module pages under docs/sdk-modules, pinned against the directories under src/Buildvana.Sdk/Modules. A page
// names its module in its title, as in "# `Wine` module". A test fails when a module has no page, a page names no
// module, or the index of docs/ leaves a page out. The repository root reaches the tests as SdkDiagnosticsPageTests
// reads it.
internal sealed partial class ModulePagesTests
{
    private static readonly string RepositoryRoot = typeof(ModulePagesTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "RepositoryRoot")
        .Value!;

    // Every module has one page whose title names it, and every page names a module.
    [Test]
    public async Task Pages_NameTheModulesOfTheSdk()
    {
        var titled = PageFiles().Select(TitledModule);
        var modulesDirectory = Path.Combine(RepositoryRoot, "src", "Buildvana.Sdk", "Modules");
        var modules = Directory.EnumerateDirectories(modulesDirectory).Select(static path => Path.GetFileName(path));

        await Assert.That(Join(titled)).IsEqualTo(Join(modules));
    }

    // The index links every page, and links no page that does not exist. A repeated link counts once.
    [Test]
    public async Task Index_LinksEveryPage()
    {
        var index = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot, "docs", "README.md")).ConfigureAwait(false);
        var linked = IndexLinkRegex().Matches(index)
            .Select(static match => match.Groups["file"].Value)
            .Distinct(StringComparer.Ordinal);
        var pages = PageFiles().Select(static path => Path.GetFileName(path));

        await Assert.That(Join(linked)).IsEqualTo(Join(pages));
    }

    private static IEnumerable<string> PageFiles()
        => Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "docs", "sdk-modules"), "*.md");

    // The module a page names in its title, or the title itself when it names none.
    private static string TitledModule(string path)
    {
        var title = DocumentationPage.Load(path).GetHeadings(1).Single();
        return TitleRegex().Match(title) is { Success: true } match ? match.Groups["name"].Value : title;
    }

    private static string Join(IEnumerable<string> items) => string.Join(", ", items.Order(StringComparer.Ordinal));

    [GeneratedRegex(@"^`(?<name>\w+)` module$", RegexOptions.CultureInvariant)]
    private static partial Regex TitleRegex();

    // The target of a link into sdk-modules/, up to the closing parenthesis or an anchor.
    [GeneratedRegex(@"\]\(sdk-modules/(?<file>[^)#]+)", RegexOptions.CultureInvariant)]
    private static partial Regex IndexLinkRegex();
}
