// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;

// The "Dependency management" section of docs/tool-diagnostics.md, pinned against DiagnosticCodes. The test
// fails when a code exists in the class and not on the page, or on the page and not in the class. The repository
// root reaches the test through an assembly metadata item the test project supplies, as
// RepositoryConfigFilesTests of Buildvana.Core.Configuration.Tests reads it.
internal sealed class ToolDiagnosticsPageTests
{
    private const string SectionHeading = "Dependency management (1200-1299)";

    private static readonly string RepositoryRoot = typeof(ToolDiagnosticsPageTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "RepositoryRoot")
        .Value!;

    [Test]
    public async Task DependencyManagementSection_ListsTheCodesOfDiagnosticCodes()
    {
        var page = DocumentationPage.Load(Path.Combine(RepositoryRoot, "docs", "tool-diagnostics.md"));
        var listed = page.GetTableColumn(SectionHeading, "Code");
        var declared = typeof(DiagnosticCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(static field => (string)field.GetRawConstantValue()!);

        await Assert.That(Join(listed)).IsEqualTo(Join(declared));
    }

    private static string Join(IEnumerable<string> codes) => string.Join(", ", codes.Order(StringComparer.Ordinal));
}
