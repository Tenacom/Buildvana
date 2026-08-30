// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json.Nodes;
using Buildvana.Core.Configuration;

// The repository's own configuration files, checked against the model in this branch. The repository root
// reaches the test through an assembly metadata item the test project supplies, as Buildvana.Sdk.Tests does
// for the real Sdk.props.
internal sealed class RepositoryConfigFilesTests
{
    private const string ExampleFileName = "buildvana.example.jsonc";

    // Above this, a description no longer fits the comment layer of a generated example.
    private const int MaxDescriptionLines = 2;

    private static readonly string RepositoryRoot = typeof(RepositoryConfigFilesTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "RepositoryRoot")
        .Value!;

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
    // generated schema rather than any file's comments, so no convention of a file can break it.
    [Test]
    public async Task Schema_DescriptionsFitTheCommentLayer()
    {
        foreach (var description in DescriptionsOf(BuildvanaJsonConfigSchema.GenerateNode()))
        {
            var lines = BuildvanaJsonConfigExample.WrapDescription(description).Count;

            await Assert.That(lines).IsLessThanOrEqualTo(MaxDescriptionLines);
        }
    }

    private static string PathOf(string fileName) => Path.Combine(RepositoryRoot, fileName);

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
