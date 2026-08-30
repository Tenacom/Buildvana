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
    private const string ToolManifestFileName = ".config/dotnet-tools.json";
    private const string ToolPackageId = "bv";

    // The one member the two configuration files are meant to disagree on: the current file pins a released
    // version, the next one points at main.
    private const string SchemaMemberName = "$schema";

    // Comments and trailing commas are what the .jsonc extension advertises, and both configuration files use
    // them. The tool manifest uses neither, and accepting them there costs nothing.
    private static readonly JsonDocumentOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

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

    // A release copies the next file over the current one with nobody reading the diff, so anything the
    // current file states and the next one does not is reverted at the next release. Comments are stated
    // too: a header true of one file alone becomes false in the promoted copy, and so does a note beside a
    // setting. The next file adds sections, differs on $schema, and matches line for line otherwise, so the
    // current file's lines appear in the next file's in the same order. Header, comments, values, and
    // ordering are guarded together by asserting that. Divergence is therefore additive only: a line the next
    // file states differently, rather than not at all, is reported as not carried, which reads like an
    // omission and is not one. $schema is the one such line, and it is excluded by name.
    [Test]
    public async Task Next_CarriesEveryLineOfCurrent()
    {
        var current = await LinesOfAsync(CurrentFileName).ConfigureAwait(false);
        var next = await LinesOfAsync(NextFileName).ConfigureAwait(false);

        await Assert.That(FirstLineNotCarried(current, next)).IsEqualTo(string.Empty);
    }

    // The line the guard above excludes, and the line the whole split rests on. The next file states settings
    // no released bv accepts, so the schema it names is the one in main. The current file is read by the
    // pinned bv, so the schema it names is the one that version shipped. Pin either at the other's value and
    // every other test here still passes, while an editor validates the file against the wrong model.
    [Test]
    public async Task ConfigurationFiles_NameTheSchemaTheyAreReadBy()
    {
        var next = await SchemaReferenceOfAsync(NextFileName).ConfigureAwait(false);
        await Assert.That(next).IsEqualTo(SchemaUrlFor("main"));

        var pinnedVersion = await PinnedToolVersionAsync().ConfigureAwait(false);
        var current = await SchemaReferenceOfAsync(CurrentFileName).ConfigureAwait(false);
        await Assert.That(current).IsEqualTo(SchemaUrlFor(pinnedVersion));
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

    private static async Task<List<string>> LinesOfAsync(string fileName)
    {
        var text = await File.ReadAllTextAsync(PathOf(fileName)).ConfigureAwait(false);

        return [.. text.ReplaceLineEndings("\n").Split('\n')];
    }

    // The first line the next file does not carry, at the position it holds in the current file, or an empty
    // string when every line is carried. Matching each line as early as the next file allows finds an
    // ordering wherever one exists, so an early match never produces a false report.
    private static string FirstLineNotCarried(List<string> current, List<string> next)
    {
        var index = 0;
        for (var i = 0; i < current.Count; i++)
        {
            if (IsSchemaMember(current[i]))
            {
                continue;
            }

            while (index < next.Count && next[index] != current[i])
            {
                index++;
            }

            if (index == next.Count)
            {
                return $"{CurrentFileName}({i + 1}) not carried by {NextFileName}: {current[i]}";
            }

            index++;
        }

        return string.Empty;
    }

    private static bool IsSchemaMember(string line)
        => line.TrimStart().StartsWith($"\"{SchemaMemberName}\"", StringComparison.Ordinal);

    // The shape bv itself rewrites at release time: a repository URL whose version segment names a tag, or a
    // branch. Stated once here, so that a check below reads as the header comment it enforces.
    private static string SchemaUrlFor(string reference)
        => $"https://raw.githubusercontent.com/Tenacom/Buildvana/{reference}/schemas/buildvana.schema.json";

    private static async Task<string> SchemaReferenceOfAsync(string fileName)
    {
        var file = await ReadJsonAsync(fileName).ConfigureAwait(false);

        return file[SchemaMemberName]!.GetValue<string>();
    }

    private static async Task<string> PinnedToolVersionAsync()
    {
        var manifest = await ReadJsonAsync(ToolManifestFileName).ConfigureAwait(false);

        return manifest["tools"]![ToolPackageId]!["version"]!.GetValue<string>();
    }

    private static async Task<JsonObject> ReadJsonAsync(string fileName)
    {
        var text = await File.ReadAllTextAsync(PathOf(fileName)).ConfigureAwait(false);

        return (JsonObject)JsonNode.Parse(text, documentOptions: ReaderOptions)!;
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
