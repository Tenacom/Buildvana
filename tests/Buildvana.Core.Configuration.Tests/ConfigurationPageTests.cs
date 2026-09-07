// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json.Nodes;
using Buildvana.Core.Configuration;
using Buildvana.Core.Testing;

// The settings table of docs/configuration-file.md, pinned against the schema BuildvanaJsonConfigSchema generates
// from the wire model. A test fails when a setting exists in the schema and not on the page, or on the page and
// not in the schema, or when the page states a default other than the one the schema states. The repository
// root reaches the tests as ToolDiagnosticsPageTests reads it.
internal sealed class ConfigurationPageTests
{
    private const string SectionHeading = "Settings";

    private static readonly string RepositoryRoot = typeof(ConfigurationPageTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "RepositoryRoot")
        .Value!;

    private static readonly JsonObject Schema = (JsonObject)BuildvanaJsonConfigSchema.GenerateNode();

    // The page promises the order of the schema, so the order is asserted along with the set.
    [Test]
    public async Task SettingsTable_ListsTheSettingsOfTheSchema()
    {
        var listed = LoadPage().GetTableColumn(SectionHeading, "Setting");
        var declared = Settings(Schema, path: string.Empty).Select(static setting => setting.Name);

        await Assert.That(Join(listed)).IsEqualTo(Join(declared));
    }

    // A setting the schema states a default for shows the same default on the page. A setting without one shows
    // whatever the page has to say, which nothing here reads.
    [Test]
    public async Task SettingsTable_StatesTheDefaultsOfTheSchema()
    {
        var page = LoadPage();
        var names = page.GetTableColumn(SectionHeading, "Setting");
        var defaults = page.GetTableColumn(SectionHeading, "Default");
        var listed = names.Zip(defaults).ToDictionary(static pair => pair.First, static pair => pair.Second, StringComparer.Ordinal);
        var declared = Settings(Schema, path: string.Empty).Where(static setting => setting.Default is not null).ToList();
        var shown = declared.Select(setting => $"{setting.Name} = {listed.GetValueOrDefault(setting.Name, "(no row)")}");
        var expected = declared.Select(static setting => $"{setting.Name} = {setting.Default}");

        await Assert.That(Join(shown)).IsEqualTo(Join(expected));
    }

    private static DocumentationPage LoadPage()
        => DocumentationPage.Load(Path.Combine(RepositoryRoot, "docs", "configuration-file.md"));

    // The settings of a schema object, in schema order, as dotted paths. A member with "properties" is a section,
    // walked for the settings inside it. Every other member is a setting, a keyed object and a dictionary
    // included, because the page lists such an object as one row and leaves its members to the page it links.
    // A member whose type occurs more than once carries a "$ref" to the one copy the exporter kept, and the copy
    // says whether the member is a section.
    private static IEnumerable<(string Name, string? Default)> Settings(JsonObject schema, string path)
    {
        foreach (var (name, node) in (JsonObject)schema["properties"]!)
        {
            var member = (JsonObject)node!;
            var memberPath = path.Length == 0 ? name : $"{path}.{name}";
            var declaring = member["$ref"] is null
                ? member
                : BuildvanaJsonConfigExample.ResolveReference(member, Schema, memberPath);
            if (declaring["properties"] is JsonObject)
            {
                foreach (var setting in Settings(declaring, memberPath))
                {
                    yield return setting;
                }
            }
            else
            {
                yield return (memberPath, RenderDefault(member["default"] ?? declaring["default"]));
            }
        }
    }

    // A default as the page writes it: the text of a string, and the JSON of anything else.
    private static string? RenderDefault(JsonNode? node)
        => node switch
        {
            null => null,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            _ => node.ToJsonString(),
        };

    private static string Join(IEnumerable<string> items) => string.Join("; ", items);
}
