// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Buildvana.Core.Json;
using Buildvana.Core.Json.Schema;

internal sealed class JsonSchemaValidatorTests
{
    private static readonly JsonSerializerOptions KeyedOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonKeyedObjectConverter() },
    };

    [Test]
    public async Task Validate_ValidInstance_ReturnsNoErrors()
    {
        var errors = Validate(
            """{"type":"object","properties":{"name":{"type":"string"}},"additionalProperties":false}""",
            """{"name":"x"}""");
        await Assert.That(errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_TypeMismatch_ReportsKindAndPointer()
    {
        var errors = Validate(
            """{"type":"object","properties":{"name":{"type":"string"}}}""",
            """{"name":42}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.TypeMismatch);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/name");
    }

    [Test]
    public async Task Validate_NullInstanceAgainstObject_ReportsTypeMismatchAtRoot()
    {
        var errors = JsonSchemaValidator.Validate(null, Schema("""{"type":"object"}"""));
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.TypeMismatch);
        await Assert.That(errors[0].JsonPointer).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Validate_DisallowedEnumValue_ReportsDisallowedValue()
    {
        var errors = Validate(
            """{"type":"object","properties":{"c":{"enum":["a","b"]}}}""",
            """{"c":"z"}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.DisallowedValue);
    }

    [Test]
    public async Task Validate_UnknownProperty_PointsAtMember()
    {
        var errors = Validate(
            """{"type":"object","properties":{},"additionalProperties":false}""",
            """{"extra":1}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.UnknownProperty);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/extra");
    }

    [Test]
    public async Task Validate_MissingRequiredProperty_ReportsMissingProperty()
    {
        var errors = Validate(
            """{"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}""",
            """{}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.MissingProperty);
    }

    [Test]
    public async Task Validate_EmptyStringUnderMinLength_ReportsTooShort()
    {
        var errors = Validate(
            """{"type":"object","properties":{"name":{"type":"string","minLength":1,"pattern":"\\S"}}}""",
            """{"name":""}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.TooShort);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/name");
    }

    [Test]
    public async Task Validate_WhitespaceAgainstNonBlankPattern_ReportsPatternMismatch()
    {
        var errors = Validate(
            """{"type":"object","properties":{"name":{"type":"string","minLength":1,"pattern":"\\S"}}}""",
            """{"name":" "}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.PatternMismatch);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/name");
    }

    [Test]
    public async Task Validate_NonBlankString_SatisfiesMinLengthAndPattern()
    {
        var errors = Validate(
            """{"type":"object","properties":{"name":{"type":"string","minLength":1,"pattern":"\\S"}}}""",
            """{"name":"x"}""");
        await Assert.That(errors.Count).IsEqualTo(0);
    }

    // String keywords never fire on a value of another kind: that failure belongs to the type keyword.
    [Test]
    public async Task Validate_StringKeywords_IgnoreNonStrings()
    {
        var errors = Validate(
            """{"type":"object","properties":{"name":{"type":"string","minLength":1,"pattern":"\\S"}}}""",
            """{"name":42}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.TypeMismatch);
    }

    [Test]
    public async Task Validate_ArrayItems_ReportPerElementPointers()
    {
        var errors = Validate(
            """{"type":"array","items":{"type":"string"}}""",
            """["a", 2, null]""");
        await Assert.That(errors.Count).IsEqualTo(2);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/1");
        await Assert.That(errors[1].JsonPointer).IsEqualTo("/2");
    }

    [Test]
    public async Task Validate_ResolvesRefAndReportsAtReferringPointer()
    {
        var errors = Validate(
            """{"type":"object","properties":{"a":{"type":"string"},"b":{"$ref":"#/properties/a"}}}""",
            """{"b":42}""");
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.TypeMismatch);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/b");
    }

    [Test]
    public async Task Validate_CircularRef_Throws()
        => await Assert.That(() => JsonSchemaValidator.Validate(
                JsonNode.Parse("123"),
                Schema("""{"$ref":"#/a","a":{"$ref":"#/a"}}""")))
            .Throws<ArgumentException>();

    [Test]
    public async Task Validate_UnresolvableRef_Throws()
        => await Assert.That(() => JsonSchemaValidator.Validate(
                JsonNode.Parse("123"),
                Schema("""{"$ref":"#/missing"}""")))
            .Throws<ArgumentException>();

    [Test]
    public async Task Validate_WithBytes_FillsLineAndColumn()
    {
        var schema = Schema("""{"type":"object","properties":{"name":{"type":"string"}}}""");
        var bytes = "{\n  \"name\": 42\n}"u8;
        var errors = JsonSchemaValidator.Validate(JsonNode.Parse(bytes), schema, bytes);
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Line).IsEqualTo(2);
        await Assert.That(errors[0].Column).IsEqualTo(11);
    }

    [Test]
    public async Task Validate_WithSourceMap_FillsLineAndColumn()
    {
        var schema = Schema("""{"type":"object","properties":{"name":{"type":"string"}}}""");
        var bytes = "{\n  \"name\": 42\n}"u8;
        var errors = JsonSchemaValidator.Validate(JsonNode.Parse(bytes), schema, JsonSourceMap.Build(bytes));
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Line).IsEqualTo(2);
        await Assert.That(errors[0].Column).IsEqualTo(11);
    }

    [Test]
    public async Task Validate_WithNullSourceMap_Throws()
        => await Assert.That(() => JsonSchemaValidator.Validate(
                JsonNode.Parse("123"),
                Schema("""{"type":"string"}"""),
                (JsonSourceMap)null!))
            .Throws<ArgumentNullException>();

    [Test]
    public async Task Validate_NumericObjectKeyVersusArrayIndex_DisambiguatesDisplayPath()
    {
        var errors = Validate(
            """{"type":"object","properties":{"obj":{"type":"object","additionalProperties":{"type":"string"}},"arr":{"type":"array","items":{"type":"string"}}}}""",
            """{"obj":{"1":true},"arr":["x",true]}""");
        await Assert.That(errors.Count).IsEqualTo(2);

        // Both offending values sit at a pointer token "1", but only the array element is an index.
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/obj/1");
        await Assert.That(errors[0].DisplayPath).IsEqualTo("obj.1");
        await Assert.That(errors[1].JsonPointer).IsEqualTo("/arr/1");
        await Assert.That(errors[1].DisplayPath).IsEqualTo("arr[1]");
    }

    // End-to-end against a generated schema: the validator must validate what the keyed-object transform of
    // JsonSchemaGenerator emits.
    [Test]
    public async Task Validate_KeyedObjectList_AcceptsValidDocument()
    {
        var instance = JsonNode.Parse("""{"policies":{"*.txt":"latest"},"groups":{"g1":{"files":"*.cs","retries":2}}}""");
        var errors = JsonSchemaValidator.Validate(instance, KeyedSchema());
        await Assert.That(errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_KeyedObjectList_ReportsWrongValueType()
    {
        var instance = JsonNode.Parse("""{"policies":{"*.txt":42}}""");
        var errors = JsonSchemaValidator.Validate(instance, KeyedSchema());
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.TypeMismatch);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/policies/*.txt");
    }

    // Restating the key inside a value object hits the Boolean 'false' subschema the generator plants there.
    [Test]
    public async Task Validate_KeyedObjectList_ReportsRestatedKey()
    {
        var instance = JsonNode.Parse("""{"groups":{"g1":{"caption":"g1"}}}""");
        var errors = JsonSchemaValidator.Validate(instance, KeyedSchema());
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.ValueNotAllowed);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/groups/g1/caption");
    }

    // A key outside the [JsonAllowedKeys] set of a constrained keyed list hits the closed key set the
    // generator plants: explicit properties plus additionalProperties: false.
    [Test]
    public async Task Validate_ConstrainedKeyedList_ReportsDisallowedKey()
    {
        var instance = JsonNode.Parse("""{"limitedPolicies":{"third":"latest"}}""");
        var errors = JsonSchemaValidator.Validate(instance, KeyedSchema());
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Kind).IsEqualTo(JsonSchemaErrorKind.UnknownProperty);
        await Assert.That(errors[0].JsonPointer).IsEqualTo("/limitedPolicies/third");
    }

    private static JsonNode Schema(string json) => JsonNode.Parse(json)!;

    private static JsonNode KeyedSchema() => JsonSchemaGenerator.Generate<KeyedSchemaSample>(KeyedOptions);

    private static IReadOnlyList<JsonSchemaValidationError> Validate(string schema, string instance)
        => JsonSchemaValidator.Validate(JsonNode.Parse(instance), Schema(schema));
}
