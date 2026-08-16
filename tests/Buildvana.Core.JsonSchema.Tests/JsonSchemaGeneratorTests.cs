// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Buildvana.Core.JsonSchema;

internal sealed class JsonSchemaGeneratorTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
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

    [Test]
    public async Task Generate_KeepsNullOnNullableDictionaryValue()
    {
        var type = Generate()["properties"]!["env"]!["additionalProperties"]!["type"];
        await Assert.That(type is JsonArray).IsTrue();
        await Assert.That(((JsonArray)type!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generate_StripsNullFromNonNullableDictionaryValue()
    {
        var type = Generate()["properties"]!["vars"]!["additionalProperties"]!["type"];
        await Assert.That(type!.GetValueKind()).IsEqualTo(JsonValueKind.String);
        await Assert.That(type.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_KeepsNullOnNullableArrayElement()
    {
        var type = Generate()["properties"]!["items"]!["items"]!["type"];
        await Assert.That(type is JsonArray).IsTrue();
        await Assert.That(((JsonArray)type!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generate_StripsNullFromNonNullableArrayElement()
    {
        var type = Generate()["properties"]!["tags"]!["items"]!["type"];
        await Assert.That(type!.GetValueKind()).IsEqualTo(JsonValueKind.String);
        await Assert.That(type.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_WithoutDefaults_EmitsNoDefaults()
    {
        var schema = JsonSchemaGenerator.Generate<DefaultsSchemaSample>(Options);
        await Assert.That(schema["properties"]!["text"]!["default"]).IsNull();
    }

    // The schema comes from one type, the defaults from another: matching is by JSON name alone.
    [Test]
    public async Task Generate_WithDefaults_AnnotatesLeafProperties()
    {
        var properties = GenerateWithDefaults()["properties"]!;
        await Assert.That(properties["text"]!["default"]!.GetValue<string>()).IsEqualTo("hello");
        await Assert.That(properties["flag"]!["default"]!.GetValue<bool>()).IsTrue();
    }

    // The enum default renders exactly as the serializer options render the value ("two", not "Two" or 1),
    // so the schema's "default" always names a member of its own "enum" list.
    [Test]
    public async Task Generate_WithDefaults_RendersEnumDefaultsThroughOptions()
    {
        var level = GenerateWithDefaults()["properties"]!["level"]!;
        await Assert.That(level["default"]!.GetValue<string>()).IsEqualTo("two");
    }

    [Test]
    public async Task Generate_WithDefaults_StatesNoDefaultForNullValues()
    {
        var notStated = GenerateWithDefaults()["properties"]!["notStated"]!;
        await Assert.That(notStated["default"]).IsNull();
    }

    [Test]
    public async Task Generate_WithDefaults_HonorsNoDefaultAttribute()
    {
        var dynamic = GenerateWithDefaults()["properties"]!["dynamic"]!;
        await Assert.That(dynamic["default"]).IsNull();
    }

    [Test]
    public async Task Generate_WithDefaults_StatesNoDefaultForCollections()
    {
        var tags = GenerateWithDefaults()["properties"]!["tags"]!;
        await Assert.That(tags["default"]).IsNull();
    }

    [Test]
    public async Task Generate_WithDefaults_RecursesIntoSections()
    {
        var section = GenerateWithDefaults()["properties"]!["section"]!;
        await Assert.That(section["default"]).IsNull();
        await Assert.That(section["properties"]!["inner"]!["default"]!.GetValue<string>()).IsEqualTo("nested-default");
    }

    [Test]
    public async Task Generate_EmitsRequiredForRequiredMembers()
    {
        var schema = JsonSchemaGenerator.Generate<RequiredSample>(Options);
        var required = (JsonArray)schema["required"]!;
        await Assert.That(required.Count).IsEqualTo(2);
        await Assert.That(required[0]!.GetValue<string>()).IsEqualTo("must");
        await Assert.That(required[1]!.GetValue<string>()).IsEqualTo("mustFlag");
    }

    // `required` only checks presence, so required strings are additionally constrained to non-blank
    // values: minLength rejects the empty string, pattern rejects all-whitespace ones.
    [Test]
    public async Task Generate_ConstrainsRequiredStringsToNonBlank()
    {
        var must = JsonSchemaGenerator.Generate<RequiredSample>(Options)["properties"]!["must"]!;
        await Assert.That(must["minLength"]!.GetValue<int>()).IsEqualTo(1);
        await Assert.That(must["pattern"]!.GetValue<string>()).IsEqualTo(@"\S");
    }

    [Test]
    public async Task Generate_LeavesOptionalStringsUnconstrained()
    {
        var may = JsonSchemaGenerator.Generate<RequiredSample>(Options)["properties"]!["may"]!;
        await Assert.That(may["minLength"]).IsNull();
        await Assert.That(may["pattern"]).IsNull();
    }

    [Test]
    public async Task Generate_LeavesRequiredNonStringsUnconstrained()
    {
        var mustFlag = JsonSchemaGenerator.Generate<RequiredSample>(Options)["properties"]!["mustFlag"]!;
        await Assert.That(mustFlag["minLength"]).IsNull();
        await Assert.That(mustFlag["pattern"]).IsNull();
    }

    private static JsonNode Generate() => JsonSchemaGenerator.Generate<GeneratorSample>(Options);

    private static JsonNode GenerateWithDefaults()
        => JsonSchemaGenerator.Generate<DefaultsSchemaSample>(Options, defaults: new DefaultsValuesSample());
}
