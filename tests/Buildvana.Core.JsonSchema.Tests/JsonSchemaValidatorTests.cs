// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Nodes;
using Buildvana.Core.JsonSchema;

internal sealed class JsonSchemaValidatorTests
{
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
        var bytes = Encoding.UTF8.GetBytes("{\n  \"name\": 42\n}");
        var errors = JsonSchemaValidator.Validate(JsonNode.Parse(bytes), schema, bytes);
        await Assert.That(errors.Count).IsEqualTo(1);
        await Assert.That(errors[0].Line).IsEqualTo(2);
        await Assert.That(errors[0].Column).IsEqualTo(11);
    }

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

    private static JsonNode Schema(string json) => JsonNode.Parse(json)!;

    private static IReadOnlyList<JsonSchemaValidationError> Validate(string schema, string instance)
        => JsonSchemaValidator.Validate(JsonNode.Parse(instance), Schema(schema));
}
