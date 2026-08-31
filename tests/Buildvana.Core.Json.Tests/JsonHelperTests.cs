// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.Json;

internal sealed partial class JsonHelperTests
{
    // Opening a directory as a file raises UnauthorizedAccessException on every platform — the
    // access-denied failure mode that is not an IOException, which JsonHelper must still wrap
    // in BuildFailedException per the IJsonHelper contract.
    private static string DeniedAccessPath => Path.TrimEndingDirectorySeparator(Path.GetTempPath());

    [Test]
    public async Task LoadObject_WithDeniedAccess_Fails()
    {
        static JsonObject Act() => new JsonHelper().LoadObject(DeniedAccessPath);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not read from");
        await Assert.That(exception.InnerException).IsTypeOf<UnauthorizedAccessException>();
    }

    [Test]
    public async Task SaveObject_WithDeniedAccess_Fails()
    {
        static void Act() => new JsonHelper().SaveObject(new JsonObject(), DeniedAccessPath);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not write to");
        await Assert.That(exception.InnerException).IsTypeOf<UnauthorizedAccessException>();
    }

    [Test]
    public async Task RewriteStringValues_WithDeniedAccess_Fails()
    {
        static bool Act() => new JsonHelper().RewriteStringValues(DeniedAccessPath, (_, _) => null);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not read from");
        await Assert.That(exception.InnerException).IsTypeOf<UnauthorizedAccessException>();
    }

    [Test]
    public async Task InsertProperty_WithDeniedAccess_Fails()
    {
        static bool Act() => new JsonHelper().InsertProperty(DeniedAccessPath, [], "a", JsonValue.Create("x"));

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not read from");
        await Assert.That(exception.InnerException).IsTypeOf<UnauthorizedAccessException>();
    }

    [Test]
    public async Task InsertProperty_InsertsFirst_MimickingIndentation()
    {
        using var file = new TempJsonFile(
            """
            {
              "sdk": {
                "version": "10.0.302"
              },
              "msbuild-sdks": {
                "Microsoft.Build.NoTargets": "3.7.134"
              }
            }

            """);

        var inserted = new JsonHelper().InsertProperty(file.Path, ["msbuild-sdks"], "Buildvana.Sdk", JsonValue.Create("2.1.41-preview"));

        await Assert.That(inserted).IsTrue();
        await Assert.That(file.ReadText()).IsEqualTo(
            """
            {
              "sdk": {
                "version": "10.0.302"
              },
              "msbuild-sdks": {
                "Buildvana.Sdk": "2.1.41-preview",
                "Microsoft.Build.NoTargets": "3.7.134"
              }
            }

            """);
    }

    [Test]
    public async Task InsertProperty_InsertsObjectValue_IntoRootObject()
    {
        using var file = new TempJsonFile(
            """
            {
              "sdk": {
                "version": "10.0.302"
              }
            }

            """);

        var section = new JsonObject { ["Buildvana.Sdk"] = "2.1.41-preview" };
        var inserted = new JsonHelper().InsertProperty(file.Path, [], "msbuild-sdks", section);

        await Assert.That(inserted).IsTrue();
        await Assert.That(file.ReadText()).IsEqualTo(
            """
            {
              "msbuild-sdks": {
                "Buildvana.Sdk": "2.1.41-preview"
              },
              "sdk": {
                "version": "10.0.302"
              }
            }

            """);
    }

    [Test]
    public async Task InsertProperty_ExpandsEmptyObject()
    {
        using var file = new TempJsonFile(
            """
            {
              "msbuild-sdks": {}
            }

            """);

        var inserted = new JsonHelper().InsertProperty(file.Path, ["msbuild-sdks"], "Buildvana.Sdk", JsonValue.Create("2.1.41-preview"));

        await Assert.That(inserted).IsTrue();
        await Assert.That(file.ReadText()).IsEqualTo(
            """
            {
              "msbuild-sdks": {
                "Buildvana.Sdk": "2.1.41-preview"
              }
            }

            """);
    }

    [Test]
    public async Task InsertProperty_SplicesIntoSingleLineObject()
    {
        using var file = new TempJsonFile("""{"a": 1}""");

        var inserted = new JsonHelper().InsertProperty(file.Path, [], "b", JsonValue.Create("x"));

        await Assert.That(inserted).IsTrue();
        await Assert.That(file.ReadText()).IsEqualTo("""{"b": "x", "a": 1}""");
    }

    [Test]
    public async Task InsertProperty_WithExistingProperty_LeavesFileUntouched()
    {
        const string content = """
            {
              "msbuild-sdks": {
                "Buildvana.Sdk": "2.1.40-preview"
              }
            }

            """;
        using var file = new TempJsonFile(content);

        var inserted = new JsonHelper().InsertProperty(file.Path, ["msbuild-sdks"], "Buildvana.Sdk", JsonValue.Create("2.1.41-preview"));

        await Assert.That(inserted).IsFalse();
        await Assert.That(file.ReadText()).IsEqualTo(content);
    }

    [Test]
    public async Task InsertProperty_WithoutParentObject_Fails()
    {
        using var file = new TempJsonFile("""{"sdk": {}}""");
        var path = file.Path;

        bool Act() => new JsonHelper().InsertProperty(path, ["msbuild-sdks"], "Buildvana.Sdk", JsonValue.Create("x"));

        await Assert.That(Act).Throws<BuildFailedException>();
    }

    [Test]
    public async Task InsertProperty_WithParentThatIsAnArray_Fails()
    {
        using var file = new TempJsonFile("""{"msbuild-sdks": [{"a": 1}]}""");
        var path = file.Path;

        bool Act() => new JsonHelper().InsertProperty(path, ["msbuild-sdks"], "Buildvana.Sdk", JsonValue.Create("x"));

        await Assert.That(Act).Throws<BuildFailedException>();
    }

    [Test]
    public async Task InsertProperty_WithInvalidJson_Fails()
    {
        using var file = new TempJsonFile("{ not json");
        var path = file.Path;

        bool Act() => new JsonHelper().InsertProperty(path, [], "a", JsonValue.Create("x"));

        await Assert.That(Act).Throws<BuildFailedException>();
    }

    [Test]
    public async Task InsertProperty_PreservesComments()
    {
        using var file = new TempJsonFile(
            """
            {
              // keep me
              "a": 1
            }
            """);

        _ = new JsonHelper().InsertProperty(file.Path, [], "b", JsonValue.Create("x"));

        await Assert.That(file.ReadText()).IsEqualTo(
            """
            {
              "b": "x",
              // keep me
              "a": 1
            }
            """);
    }

    [Test]
    public async Task InsertProperty_PreservesCrLfLineEndings()
    {
        using var file = new TempJsonFile("{\r\n  \"a\": 1\r\n}\r\n");

        _ = new JsonHelper().InsertProperty(file.Path, [], "b", JsonValue.Create("x"));

        await Assert.That(file.ReadText()).IsEqualTo("{\r\n  \"b\": \"x\",\r\n  \"a\": 1\r\n}\r\n");
    }

    [Test]
    public async Task InsertProperty_UsesCrLfInMultiLineValues_WhenFileUsesCrLf()
    {
        using var file = new TempJsonFile("{\r\n  \"a\": 1\r\n}\r\n");

        var section = new JsonObject { ["x"] = "y" };
        _ = new JsonHelper().InsertProperty(file.Path, [], "b", section);

        await Assert.That(file.ReadText()).IsEqualTo("{\r\n  \"b\": {\r\n    \"x\": \"y\"\r\n  },\r\n  \"a\": 1\r\n}\r\n");
    }

    [Test]
    public async Task RewriteBooleanValues_SplicesTheLiteral_LeavingEverythingElseAlone()
    {
        using var file = new TempJsonFile(
            """
            {
              // a comment nothing may touch
              "sdk": {
                "version": "10.0.302",
                "allowPrerelease": false
              }
            }

            """);

        var rewritten = new JsonHelper().RewriteBooleanValues(file.Path, static (path, _) => path is ["sdk", "allowPrerelease"] ? true : null);

        await Assert.That(rewritten).IsTrue();
        await Assert.That(file.ReadText()).IsEqualTo(
            """
            {
              // a comment nothing may touch
              "sdk": {
                "version": "10.0.302",
                "allowPrerelease": true
              }
            }

            """);
    }

    [Test]
    public async Task RewriteBooleanValues_LeavingEveryValueAlone_WritesNothing()
    {
        const string content = """{"a": true, "b": false}""";
        using var file = new TempJsonFile(content);

        var rewritten = new JsonHelper().RewriteBooleanValues(file.Path, static (_, current) => current);

        await Assert.That(rewritten).IsFalse();
        await Assert.That(file.ReadText()).IsEqualTo(content);
    }

    // A boolean inside an array has no property name, so no rewriter can name it either.
    [Test]
    public async Task RewriteBooleanValues_LeavesArrayElementsAlone()
    {
        const string content = """{"flags": [true, false]}""";
        using var file = new TempJsonFile(content);

        var rewritten = new JsonHelper().RewriteBooleanValues(file.Path, static (_, current) => !current);

        await Assert.That(rewritten).IsFalse();
        await Assert.That(file.ReadText()).IsEqualTo(content);
    }

    [Test]
    public async Task RewriteBooleanValues_OfInvalidJson_Fails()
    {
        using var file = new TempJsonFile("{ not json");

        bool Act() => new JsonHelper().RewriteBooleanValues(file.Path, static (_, current) => !current);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("does not contain valid JSON");
    }

    [Test]
    public async Task InsertProperty_PreservesUtf8Bom()
    {
        var contentBytes = "{\n  \"a\": 1\n}\n"u8.ToArray();
        using var file = new TempJsonFile([0xEF, 0xBB, 0xBF, .. contentBytes]);

        _ = new JsonHelper().InsertProperty(file.Path, [], "b", JsonValue.Create("x"));

        var bytes = await File.ReadAllBytesAsync(file.Path).ConfigureAwait(false);
        await Assert.That(bytes[..3]).IsEquivalentTo(new byte[] { 0xEF, 0xBB, 0xBF });
        await Assert.That(Encoding.UTF8.GetString(bytes[3..])).IsEqualTo("{\n  \"b\": \"x\",\n  \"a\": 1\n}\n");
    }
}
