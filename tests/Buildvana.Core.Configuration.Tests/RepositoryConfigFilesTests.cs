// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Buildvana.Core.Configuration;

// The repository's own configuration files, checked against the model in this branch. The repository root
// reaches the test through an assembly metadata item the test project supplies, as Buildvana.Sdk.Tests does
// for the real Sdk.props.
internal sealed class RepositoryConfigFilesTests
{
    private const string CurrentFileName = "buildvana.jsonc";
    private const string NextFileName = "buildvana.next.jsonc";
    private const string ExampleFileName = "buildvana.example.jsonc";

    // The one member the two configuration files are meant to disagree on: the current file pins a released
    // version, the next one points at main.
    private const string SchemaMemberName = "$schema";

    private static readonly string RepositoryRoot = typeof(RepositoryConfigFilesTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "RepositoryRoot")
        .Value!;

    // The schema built from the model in this branch, which is what LoadFile validates against too. No
    // committed schema file is read here: a rebase moves the model, and every check below moves with it.
    private static readonly JsonObject Schema = (JsonObject)BuildvanaJsonConfigSchema.GenerateNode();

    // The next file states settings the pinned bv rejects, so no bv run in this repository validates it. The
    // answer to "against which schema" is: against the model in this branch, which is what LoadFile builds
    // its schema from. A rebase moves the model, and the check moves with it.
    [Test]
    public async Task Next_IsAcceptedByTheLoader()
        => await Assert.That(() => BuildvanaJsonConfigProvider.LoadFile(PathOf(NextFileName))).ThrowsNothing();

    // A release copies the next file over the current one with nobody reading the diff, so an edit made to
    // one file and not the other would be reverted at the next release. This is the guard against that.
    [Test]
    public async Task Next_StatesEverythingCurrentStates()
    {
        var current = await ReadJsonAsync(CurrentFileName).ConfigureAwait(false);
        var next = await ReadJsonAsync(NextFileName).ConfigureAwait(false);

        var divergences = Divergences(current, next, Schema, path: string.Empty).ToList();

        await Assert.That(string.Join(", ", divergences)).IsEqualTo(string.Empty);
    }

    // A release copies the next file over the current one, comments included, so a header true of only one of
    // the two becomes false in the promoted copy. The two headers are therefore one text.
    [Test]
    public async Task Next_SharesTheHeaderOfCurrent()
    {
        var current = await HeaderOfAsync(CurrentFileName).ConfigureAwait(false);
        var next = await HeaderOfAsync(NextFileName).ConfigureAwait(false);

        await Assert.That(next).IsEqualTo(current);
    }

    // The committed example is a build artifact under review: it drifts the moment the model changes, and
    // nothing else in this repository would notice.
    [Test]
    public async Task Example_MatchesTheGeneratedText()
    {
        var text = await File.ReadAllTextAsync(PathOf(ExampleFileName)).ConfigureAwait(false);
        var committed = text.ReplaceLineEndings("\n");

        await Assert.That(committed).IsEqualTo(BuildvanaJsonConfigExample.Generate());
    }

    // An example nothing can load is a worked example of nothing.
    [Test]
    public async Task Example_IsAcceptedByTheLoader()
        => await Assert.That(() => BuildvanaJsonConfigProvider.LoadFile(PathOf(ExampleFileName))).ThrowsNothing();

    // A description names a setting; anything longer is documentation in an editor tooltip. This reads the
    // generated schema rather than any file's comments, so no convention of a file can break it. The
    // offenders are collected rather than asserted one at a time, so that a failure names the text.
    [Test]
    public async Task Schema_DescriptionsFitTheCommentLayer()
    {
        List<string> problems = [];
        foreach (var description in DescriptionsOf(Schema))
        {
            if (BuildvanaJsonConfigExample.DescriptionProblem(description) is { } problem)
            {
                problems.Add($"\"{description}\" {problem}");
            }
        }

        await Assert.That(string.Join("; ", problems)).IsEqualTo(string.Empty);
    }

    private static string PathOf(string fileName) => Path.Combine(RepositoryRoot, fileName);

    private static async Task<JsonObject> ReadJsonAsync(string fileName)
    {
        var text = await File.ReadAllTextAsync(PathOf(fileName)).ConfigureAwait(false);
        var documentOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return (JsonObject)JsonNode.Parse(text, documentOptions: documentOptions)!;
    }

    // Everything a file states before its opening brace, which is the part a promotion copies verbatim.
    private static async Task<string> HeaderOfAsync(string fileName)
    {
        var text = await File.ReadAllTextAsync(PathOf(fileName)).ConfigureAwait(false);
        var normalized = text.ReplaceLineEndings("\n");

        return normalized[..normalized.IndexOf('{', StringComparison.Ordinal)];
    }

    // Reports every member the current file states that the next file does not match. A member the schema no
    // longer declares is skipped: the model may have dropped it or renamed it, and the two files are then
    // free to differ on it. A member the next file adds is not a divergence — adding is what it is for.
    private static IEnumerable<string> Divergences(JsonObject current, JsonObject next, JsonObject schema, string path)
    {
        foreach (var (name, currentValue) in current)
        {
            if (path.Length == 0 && name == SchemaMemberName)
            {
                continue;
            }

            var memberPath = path.Length == 0 ? name : path + "." + name;
            if (DeclaredSchema(schema, name, memberPath) is not { } memberSchema)
            {
                continue;
            }

            var nextValue = next[name];
            if (nextValue is null)
            {
                yield return memberPath + " (unstated)";
            }
            else if (currentValue is JsonObject currentSection && nextValue is JsonObject nextSection)
            {
                foreach (var divergence in Divergences(currentSection, nextSection, memberSchema, memberPath))
                {
                    yield return divergence;
                }
            }
            else if (!JsonNode.DeepEquals(currentValue, nextValue))
            {
                yield return memberPath;
            }
        }
    }

    // The subschema governing one member, or null when the schema no longer declares it. A keyed object and a
    // dictionary declare their members through additionalProperties, their member names being data.
    private static JsonObject? DeclaredSchema(JsonObject schema, string name, string path)
    {
        var resolved = schema["$ref"] is null ? schema : BuildvanaJsonConfigExample.ResolveReference(schema, Schema, path);
        return (resolved["properties"] as JsonObject)?[name] as JsonObject
            ?? resolved["additionalProperties"] as JsonObject;
    }

    // Every "description" keyword in a schema document, wherever it sits.
    private static IEnumerable<string> DescriptionsOf(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject members:
                foreach (var (name, value) in members)
                {
                    if (name == "description" && value is JsonValue text)
                    {
                        yield return text.GetValue<string>();
                    }
                    else
                    {
                        foreach (var description in DescriptionsOf(value))
                        {
                            yield return description;
                        }
                    }
                }

                break;
            case JsonArray items:
                foreach (var item in items)
                {
                    foreach (var description in DescriptionsOf(item))
                    {
                        yield return description;
                    }
                }

                break;
        }
    }
}
