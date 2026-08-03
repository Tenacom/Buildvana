// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.Json;

internal sealed partial class JsonHelperTests
{
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
        var content = """
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

        var act = () => new JsonHelper().InsertProperty(path, ["msbuild-sdks"], "Buildvana.Sdk", JsonValue.Create("x"));

        await Assert.That(act).Throws<BuildFailedException>();
    }

    [Test]
    public async Task InsertProperty_WithParentThatIsAnArray_Fails()
    {
        using var file = new TempJsonFile("""{"msbuild-sdks": [{"a": 1}]}""");
        var path = file.Path;

        var act = () => new JsonHelper().InsertProperty(path, ["msbuild-sdks"], "Buildvana.Sdk", JsonValue.Create("x"));

        await Assert.That(act).Throws<BuildFailedException>();
    }

    [Test]
    public async Task InsertProperty_WithInvalidJson_Fails()
    {
        using var file = new TempJsonFile("{ not json");
        var path = file.Path;

        var act = () => new JsonHelper().InsertProperty(path, [], "a", JsonValue.Create("x"));

        await Assert.That(act).Throws<BuildFailedException>();
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
    public async Task InsertProperty_PreservesUtf8Bom()
    {
        var contentBytes = Encoding.UTF8.GetBytes("{\n  \"a\": 1\n}\n");
        using var file = new TempJsonFile([0xEF, 0xBB, 0xBF, .. contentBytes]);

        _ = new JsonHelper().InsertProperty(file.Path, [], "b", JsonValue.Create("x"));

        var bytes = await File.ReadAllBytesAsync(file.Path).ConfigureAwait(false);
        await Assert.That(bytes[..3]).IsEquivalentTo(new byte[] { 0xEF, 0xBB, 0xBF });
        await Assert.That(Encoding.UTF8.GetString(bytes[3..])).IsEqualTo("{\n  \"b\": \"x\",\n  \"a\": 1\n}\n");
    }
}
