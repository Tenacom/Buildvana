// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Buildvana.Core.JsonSchema;

internal sealed class JsonSchemaGeneratorTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Test]
    public async Task Generate_EmitsDialectAndTitle()
    {
        var schema = Generate();
        await Assert.That(schema["$schema"]!.GetValue<string>())
            .IsEqualTo("https://json-schema.org/draft/2020-12/schema");
        await Assert.That(schema["title"]!.GetValue<string>()).IsEqualTo("Sample Title");
    }

    [Test]
    public async Task Generate_StripsNullFromPlainNullableProperty()
    {
        var type = Generate()["properties"]!["plain"]!["type"];
        await Assert.That(type!.GetValueKind()).IsEqualTo(JsonValueKind.String);
        await Assert.That(type.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_KeepsNullWhenPropertyIsJsonNullable()
    {
        var type = Generate()["properties"]!["maybe"]!["type"];
        await Assert.That(type is JsonArray).IsTrue();
        await Assert.That(((JsonArray)type!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generate_SurfacesDescription()
    {
        var description = Generate()["properties"]!["described"]!["description"];
        await Assert.That(description!.GetValue<string>()).IsEqualTo("a described field");
    }

    [Test]
    public async Task Generate_ConstrainsDictionaryToAllowedKeys()
    {
        var map = Generate()["properties"]!["map"]!;
        await Assert.That(map["additionalProperties"]!.GetValue<bool>()).IsFalse();
        await Assert.That(map["properties"]!["alpha"]).IsNotNull();
        await Assert.That(map["properties"]!["beta"]).IsNotNull();
        await Assert.That((map["properties"] as JsonObject)!.Count).IsEqualTo(2);
    }

    private static JsonNode Generate() => JsonSchemaGenerator.Generate<GeneratorSample>(Options);
}
