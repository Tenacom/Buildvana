// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;

// The module pages under docs/04-sdk/05-modules, pinned against the directories under src/Buildvana.Sdk/Modules. The
// title of a page, in its front matter, is the name of its module, as in "title: Wine". The test fails when a
// module has no page, or a page names no module. The repository root reaches the test as SdkDiagnosticsPageTests
// reads it.
internal sealed class ModulePagesTests
{
    private static readonly string RepositoryRoot = typeof(ModulePagesTests).Assembly.GetMetadata("RepositoryRoot");

    // Every module has one page whose title names it, and every page names a module.
    [Test]
    public async Task Pages_NameTheModulesOfTheSdk()
    {
        var titled = PageFiles().Select(TitledModule);
        var modulesDirectory = Path.Combine(RepositoryRoot, "src", "Buildvana.Sdk", "Modules");
        var modules = Directory.EnumerateDirectories(modulesDirectory).Select(static path => Path.GetFileName(path));

        await Assert.That(Join(titled)).IsEqualTo(Join(modules));
    }

    private static IEnumerable<string> PageFiles()
        => Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "docs", "04-sdk", "05-modules"), "*.md");

    // The title of a page, or its file name when the page has no title, so that the failure names the page.
    private static string TitledModule(string path)
        => DocumentationPage.Load(path).Title ?? $"{Path.GetFileName(path)} (no title)";

    private static string Join(IEnumerable<string> items) => string.Join(", ", items.Order(StringComparer.Ordinal));
}
