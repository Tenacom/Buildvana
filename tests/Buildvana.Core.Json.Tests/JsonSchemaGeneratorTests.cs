// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Buildvana.Core.Json;
using Buildvana.Core.Json.Schema;

internal sealed class JsonSchemaGeneratorTests
{
    private static readonly JsonSerializerOptions Options = new() {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new JsonKeyedObjectConverter() },
    };

    // Deliberately lacking JsonKeyedObjectConverter, to prove the keyed rendering is gated on its registration.
    private static readonly JsonSerializerOptions KeyedConverterlessOptions = new() {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Test]
    public async Task Generate_EmitsDialectAndTitle()
    {
        var schema = Generate();
        await Assert.That(schema["$schema"]?.GetValue<string>())
            .IsEqualTo("https://json-schema.org/draft/2020-12/schema");
        await Assert.That(schema["title"]?.GetValue<string>()).IsEqualTo("Sample Title");
    }

    [Test]
    public async Task Generate_StripsNullFromPlainNullableProperty()
    {
        var type = await Assert.That(Generate()["properties"]?["plain"]?["type"]).IsNotNull();
        await Assert.That(type.GetValueKind()).IsEqualTo(JsonValueKind.String);
        await Assert.That(type.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_KeepsNullWhenPropertyIsJsonNullable()
    {
        var type = await Assert.That(Generate()["properties"]?["maybe"]?["type"]).IsNotNull();
        await Assert.That(type.AsArray().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generate_SurfacesDescription()
    {
        var description = Generate()["properties"]?["described"]?["description"];
        await Assert.That(description?.GetValue<string>()).IsEqualTo("a described field");
    }

    [Test]
    public async Task Generate_SurfacesExample()
    {
        var examples = await Assert.That(Generate()["properties"]?["sampled"]?["examples"]).IsNotNull();
        await Assert.That(examples.AsArray().Count).IsEqualTo(1);
        await Assert.That(examples[0]?.ToJsonString()).IsEqualTo("""["alpha","beta"]""");
    }

    // Both annotations insert at the head of the node, so the one applied last comes first.
    [Test]
    public async Task Generate_PutsDescriptionBeforeExample()
    {
        var sampled = await Assert.That(Generate()["properties"]?["sampled"]).IsNotNull();
        var firstTwo = sampled.AsObject().Select(static member => member.Key).Take(2);
        await Assert.That(firstTwo.SequenceEqual(["description", "examples"])).IsTrue();
    }

    [Test]
    public async Task Generate_ThrowsOnUnparseableExample()
        => await Assert.That(() => JsonSchemaGenerator.Generate<BadExampleSample>(Options))
            .Throws<InvalidOperationException>();

    [Test]
    public async Task Generate_ConstrainsDictionaryToAllowedKeys()
    {
        var map = await Assert.That(Generate()["properties"]?["map"]).IsNotNull();
        await Assert.That(map["additionalProperties"]?.GetValue<bool>()).IsFalse();
        await Assert.That(map["properties"]?["alpha"]).IsNotNull();
        await Assert.That(map["properties"]?["beta"]).IsNotNull();
        await Assert.That(map["properties"]?.AsObject().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generate_ConstrainsStringToAllowedValues()
    {
        var choice = await Assert.That(Generate()["properties"]?["choice"]).IsNotNull();
        await Assert.That(choice["type"]?.GetValue<string>()).IsEqualTo("string");
        var allowed = await Assert.That(choice["enum"]).IsNotNull();
        await Assert.That(allowed.AsArray().Select(static value => value?.GetValue<string>()).SequenceEqual(["alpha", "beta"])).IsTrue();
    }

    // An enumerated set is the whole value space, so a member that keeps "null" in its type has to keep null
    // in its set: otherwise the schema rejects the value its own type advertises.
    [Test]
    public async Task Generate_KeepsNullInAllowedValuesOfANullableMember()
    {
        var choice = await Assert.That(Generate()["properties"]?["maybeChoice"]).IsNotNull();
        var type = await Assert.That(choice["type"]).IsNotNull();
        await Assert.That(type.AsArray().Select(static t => t?.GetValue<string>()).SequenceEqual(["string", "null"])).IsTrue();

        var allowed = await Assert.That(choice["enum"]).IsNotNull();
        await Assert.That(allowed.AsArray().Count).IsEqualTo(3);
        await Assert.That(allowed[2]).IsNull();
    }

    // The enumerated set forbids the blank value the non-blank constraints exist to catch, so a required
    // member carrying one states neither minLength nor pattern.
    [Test]
    public async Task Generate_AllowedValuesReplaceNonBlankConstraints()
    {
        var properties = await Assert.That(Generate()["properties"]).IsNotNull();
        var enumerated = await Assert.That(properties["requiredChoice"]).IsNotNull();
        await Assert.That(enumerated["enum"]).IsNotNull();
        await Assert.That(enumerated["minLength"]).IsNull();
        await Assert.That(enumerated["pattern"]).IsNull();

        var open = await Assert.That(properties["requiredText"]).IsNotNull();
        await Assert.That(open["minLength"]?.GetValue<int>()).IsEqualTo(1);
        await Assert.That(open["pattern"]?.GetValue<string>()).IsEqualTo(@"\S");
    }

    [Test]
    public async Task Generate_KeepsNullOnNullableDictionaryValue()
    {
        var type = await Assert.That(Generate()["properties"]?["env"]?["additionalProperties"]?["type"]).IsNotNull();
        await Assert.That(type.AsArray().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generate_StripsNullFromNonNullableDictionaryValue()
    {
        var type = await Assert.That(Generate()["properties"]?["vars"]?["additionalProperties"]?["type"]).IsNotNull();
        await Assert.That(type.GetValueKind()).IsEqualTo(JsonValueKind.String);
        await Assert.That(type.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_KeepsNullOnNullableArrayElement()
    {
        var type = await Assert.That(Generate()["properties"]?["items"]?["items"]?["type"]).IsNotNull();
        await Assert.That(type.AsArray().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Generate_StripsNullFromNonNullableArrayElement()
    {
        var type = await Assert.That(Generate()["properties"]?["tags"]?["items"]?["type"]).IsNotNull();
        await Assert.That(type.GetValueKind()).IsEqualTo(JsonValueKind.String);
        await Assert.That(type.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_WithoutDefaults_EmitsNoDefaults()
    {
        var schema = JsonSchemaGenerator.Generate<DefaultsSchemaSample>(Options);
        var text = await Assert.That(schema["properties"]?["text"]).IsNotNull();
        await Assert.That(text["default"]).IsNull();
    }

    // The schema comes from one type, the defaults from another: matching is by JSON name alone.
    [Test]
    public async Task Generate_WithDefaults_AnnotatesLeafProperties()
    {
        var properties = await Assert.That(GenerateWithDefaults()["properties"]).IsNotNull();
        await Assert.That(properties["text"]?["default"]?.GetValue<string>()).IsEqualTo("hello");
        await Assert.That(properties["flag"]?["default"]?.GetValue<bool>()).IsTrue();
    }

    // The enum default renders exactly as the serializer options render the value ("two", not "Two" or 1),
    // so the schema's "default" always names a member of its own "enum" list.
    [Test]
    public async Task Generate_WithDefaults_RendersEnumDefaultsThroughOptions()
    {
        var level = await Assert.That(GenerateWithDefaults()["properties"]?["level"]).IsNotNull();
        await Assert.That(level["default"]?.GetValue<string>()).IsEqualTo("two");
    }

    [Test]
    public async Task Generate_WithDefaults_StatesNoDefaultForNullValues()
    {
        var notStated = await Assert.That(GenerateWithDefaults()["properties"]?["notStated"]).IsNotNull();
        await Assert.That(notStated["default"]).IsNull();
    }

    [Test]
    public async Task Generate_WithDefaults_HonorsNoDefaultAttribute()
    {
        var dynamic = await Assert.That(GenerateWithDefaults()["properties"]?["dynamic"]).IsNotNull();
        await Assert.That(dynamic["default"]).IsNull();
    }

    // A defaults instance that cannot answer for a schema member is a modeling error: matching is by JSON
    // name alone, so skipping the miss is how a rename on either side of a model pair would silently cost a
    // whole section its defaults.
    [Test]
    public async Task Generate_WithDefaults_ThrowsOnUnmatchedSchemaProperty()
    {
        await Assert.That(() => JsonSchemaGenerator.Generate<RequiredSample>(Options, defaults: new DefaultsValuesSample()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Generate_WithDefaults_StatesNoDefaultForCollections()
    {
        var tags = await Assert.That(GenerateWithDefaults()["properties"]?["tags"]).IsNotNull();
        await Assert.That(tags["default"]).IsNull();
    }

    [Test]
    public async Task Generate_WithDefaults_RecursesIntoSections()
    {
        var section = await Assert.That(GenerateWithDefaults()["properties"]?["section"]).IsNotNull();
        await Assert.That(section["default"]).IsNull();
        await Assert.That(section["properties"]?["inner"]?["default"]?.GetValue<string>()).IsEqualTo("nested-default");
    }

    [Test]
    public async Task Generate_EmitsRequiredForRequiredMembers()
    {
        var schema = JsonSchemaGenerator.Generate<RequiredSample>(Options);
        var required = await Assert.That(schema["required"]).IsNotNull();
        await Assert.That(required.AsArray().Count).IsEqualTo(2);
        await Assert.That(required[0]?.GetValue<string>()).IsEqualTo("must");
        await Assert.That(required[1]?.GetValue<string>()).IsEqualTo("mustFlag");
    }

    // `required` only checks presence, so required strings are additionally constrained to non-blank
    // values: minLength rejects the empty string, pattern rejects all-whitespace ones.
    [Test]
    public async Task Generate_ConstrainsRequiredStringsToNonBlank()
    {
        var must = await Assert.That(JsonSchemaGenerator.Generate<RequiredSample>(Options)["properties"]?["must"]).IsNotNull();
        await Assert.That(must["minLength"]?.GetValue<int>()).IsEqualTo(1);
        await Assert.That(must["pattern"]?.GetValue<string>()).IsEqualTo(@"\S");
    }

    [Test]
    public async Task Generate_LeavesOptionalStringsUnconstrained()
    {
        var may = await Assert.That(JsonSchemaGenerator.Generate<RequiredSample>(Options)["properties"]?["may"]).IsNotNull();
        await Assert.That(may["minLength"]).IsNull();
        await Assert.That(may["pattern"]).IsNull();
    }

    [Test]
    public async Task Generate_LeavesRequiredNonStringsUnconstrained()
    {
        var mustFlag = await Assert.That(JsonSchemaGenerator.Generate<RequiredSample>(Options)["properties"]?["mustFlag"]).IsNotNull();
        await Assert.That(mustFlag["minLength"]).IsNull();
        await Assert.That(mustFlag["pattern"]).IsNull();
    }

    [Test]
    public async Task Generate_RendersValueShapeKeyedListAsObject()
    {
        var policies = await Assert.That(GenerateKeyed()["properties"]?["policies"]).IsNotNull();
        await Assert.That(policies["type"]?.GetValue<string>()).IsEqualTo("object");

        // The value property's schema, required-string constraints included, becomes additionalProperties.
        var values = await Assert.That(policies["additionalProperties"]).IsNotNull();
        await Assert.That(values["type"]?.GetValue<string>()).IsEqualTo("string");
        await Assert.That(values["minLength"]?.GetValue<int>()).IsEqualTo(1);
    }

    [Test]
    public async Task Generate_RendersRemainingMembersKeyedListAsObject()
    {
        var groups = await Assert.That(GenerateKeyed()["properties"]?["groups"]).IsNotNull();
        await Assert.That(groups["type"]?.GetValue<string>()).IsEqualTo("object");

        var element = await Assert.That(groups["additionalProperties"]).IsNotNull();
        await Assert.That(element["type"]?.GetValue<string>()).IsEqualTo("object");
        await Assert.That(element["properties"]?["files"]).IsNotNull();
        await Assert.That(element["properties"]?["retries"]).IsNotNull();
    }

    // The key travels as the JSON property name: the converter refuses an element value that restates it,
    // so the schema forbids it with a Boolean 'false' subschema.
    [Test]
    public async Task Generate_ForbidsKeyInsideRemainingMembers()
    {
        var element = await Assert.That(GenerateKeyed()["properties"]?["groups"]?["additionalProperties"]).IsNotNull();
        await Assert.That(element["properties"]?["caption"]?.GetValue<bool>()).IsFalse();
    }

    // The key travels as the member name, where a property's own constraints cannot reach it, so a required
    // key states the non-blank discipline through propertyNames instead.
    [Test]
    public async Task Generate_ConstrainsRequiredKeyThroughPropertyNames()
    {
        var schema = GenerateKeyed();

        var valueShape = await Assert.That(schema["properties"]?["policies"]?["propertyNames"]).IsNotNull();
        await Assert.That(valueShape["minLength"]?.GetValue<int>()).IsEqualTo(1);
        await Assert.That(valueShape["pattern"]?.GetValue<string>()).IsEqualTo(@"\S");

        var remainingMembersShape = await Assert.That(schema["properties"]?["groups"]?["propertyNames"]).IsNotNull();
        await Assert.That(remainingMembersShape["minLength"]?.GetValue<int>()).IsEqualTo(1);
        await Assert.That(remainingMembersShape["pattern"]?.GetValue<string>()).IsEqualTo(@"\S");
    }

    // A key the model does not require states nothing about its own value, member name included. The node
    // that would carry such a statement is emitted all the same: it is what marks the object as keyed, and
    // an empty schema constrains no name.
    [Test]
    public async Task Generate_LeavesAnOptionalKeyUnconstrained()
    {
        var propertyNames = await Assert.That(GenerateKeyed()["properties"]?["optionalKeys"]?["propertyNames"]).IsNotNull();

        await Assert.That(propertyNames.AsObject().Count).IsEqualTo(0);
    }

    // An example on the key is an example of a member name, so it lands in propertyNames; one on the value
    // property rides the schema lifted into additionalProperties.
    [Test]
    public async Task Generate_RoutesKeyAndValueExamplesToTheirOwnNodes()
    {
        var exemplified = await Assert.That(GenerateKeyed()["properties"]?["exemplified"]).IsNotNull();

        var keyExamples = await Assert.That(exemplified["propertyNames"]?["examples"]).IsNotNull();
        await Assert.That(keyExamples[0]?.GetValue<string>()).IsEqualTo("Some.Package.*");

        var valueExamples = await Assert.That(exemplified["additionalProperties"]?["examples"]).IsNotNull();
        await Assert.That(valueExamples[0]?.GetValue<string>()).IsEqualTo("patch");
    }

    // A description of the key describes a member name, so it travels where the example does. Both exits
    // destroy the key's own subschema: the lifted-value one discards the element schema the key hangs off, the
    // pruned-key one replaces the key with a Boolean 'false'. Both are exercised here.
    [Test]
    public async Task Generate_RoutesKeyAndValueDescriptionsToTheirOwnNodes()
    {
        var schema = GenerateKeyed();

        var exemplified = await Assert.That(schema["properties"]?["exemplified"]).IsNotNull();
        await Assert.That(exemplified["propertyNames"]?["description"]?.GetValue<string>())
            .IsEqualTo("Pattern over package ids.");
        await Assert.That(exemplified["additionalProperties"]?["description"]?.GetValue<string>())
            .IsEqualTo("Policy governing every matching pin.");

        var optionalKeys = await Assert.That(schema["properties"]?["exemplifiedOptionalKeys"]).IsNotNull();
        await Assert.That(optionalKeys["propertyNames"]?["description"]?.GetValue<string>())
            .IsEqualTo("Caption naming the group.");
    }

    // An optional key states no non-blank constraint, so its example and its description are all that fills
    // propertyNames there.
    [Test]
    public async Task Generate_PutsAnOptionalKeyExampleInPropertyNames()
    {
        var propertyNames = await Assert.That(GenerateKeyed()["properties"]?["exemplifiedOptionalKeys"]?["propertyNames"]).IsNotNull();
        await Assert.That(propertyNames["examples"]?[0]?.GetValue<string>())
            .IsEqualTo("SDK package injections");
        await Assert.That(propertyNames["minLength"]).IsNull();
    }

    [Test]
    public async Task Generate_PrunesKeyFromRequiredAndKeepsTheRest()
    {
        var schema = GenerateKeyed();
        var groups = await Assert.That(schema["properties"]?["groups"]?["additionalProperties"]).IsNotNull();
        await Assert.That(groups["required"]).IsNull();

        var requiredGroups = await Assert.That(schema["properties"]?["requiredGroups"]?["additionalProperties"]).IsNotNull();
        var required = await Assert.That(requiredGroups["required"]).IsNotNull();
        await Assert.That(required.AsArray().Count).IsEqualTo(1);
        await Assert.That(required[0]?.GetValue<string>()).IsEqualTo("files");
    }

    [Test]
    public async Task Generate_RendersNestedKeyedList()
    {
        var policies = await Assert.That(GenerateKeyed()["properties"]?["nested"]?["additionalProperties"]?["properties"]?["policies"])
            .IsNotNull();
        await Assert.That(policies["type"]?.GetValue<string>()).IsEqualTo("object");
        await Assert.That(policies["additionalProperties"]?["type"]?.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_KeepsDescriptionOnKeyedList()
    {
        var description = GenerateKeyed()["properties"]?["policies"]?["description"];
        await Assert.That(description?.GetValue<string>()).IsEqualTo("the policies");
    }

    [Test]
    public async Task Generate_KeepsNullOnJsonNullableKeyedList()
    {
        var type = await Assert.That(GenerateKeyed()["properties"]?["maybePolicies"]?["type"]).IsNotNull();
        await Assert.That(type.AsArray().Count).IsEqualTo(2);
        await Assert.That(type[0]?.GetValue<string>()).IsEqualTo("object");
        await Assert.That(type[1]?.GetValue<string>()).IsEqualTo("null");
    }

    // A keyed-object list is dictionary-valued in JSON: [JsonAllowedKeys] closes its key set the same way it
    // closes a dictionary's.
    [Test]
    public async Task Generate_ConstrainsKeyedListToAllowedKeys()
    {
        var limited = await Assert.That(GenerateKeyed()["properties"]?["limitedPolicies"]).IsNotNull();
        await Assert.That(limited["additionalProperties"]?.GetValue<bool>()).IsFalse();
        var properties = await Assert.That(limited["properties"]).IsNotNull();
        await Assert.That(properties.AsObject().Count).IsEqualTo(2);
        await Assert.That(properties["first"]?["type"]?.GetValue<string>()).IsEqualTo("string");
        await Assert.That(properties["second"]?["type"]?.GetValue<string>()).IsEqualTo("string");
    }

    [Test]
    public async Task Generate_RendersKeyedListAtRoot()
    {
        var schema = JsonSchemaGenerator.Generate<IReadOnlyList<KeyedValueSample>>(Options);
        await Assert.That(schema["type"]?.GetValue<string>()).IsEqualTo("object");
        await Assert.That(schema["additionalProperties"]?["type"]?.GetValue<string>()).IsEqualTo("string");
    }

    // Without JsonKeyedObjectConverter in the options, the list deserializes as a plain JSON array, and the
    // exporter's own array schema must stand.
    [Test]
    public async Task Generate_WithoutKeyedConverter_RendersListAsArray()
    {
        var schema = JsonSchemaGenerator.Generate<KeyedSchemaSample>(KeyedConverterlessOptions);
        var policies = await Assert.That(schema["properties"]?["policies"]).IsNotNull();
        await Assert.That(policies["type"]?.GetValue<string>()).IsEqualTo("array");
        await Assert.That(policies["items"]?["type"]?.GetValue<string>()).IsEqualTo("object");
    }

    // Object-shaped in the schema, but still a collection: no default, and no recursion into it.
    [Test]
    public async Task Generate_WithDefaults_StatesNoDefaultForKeyedObjectLists()
    {
        var policies = await Assert.That(GenerateWithDefaults()["properties"]?["policies"]).IsNotNull();
        await Assert.That(policies["default"]).IsNull();
        var values = await Assert.That(policies["additionalProperties"]).IsNotNull();
        await Assert.That(values["default"]).IsNull();
    }

    [Test]
    public async Task Generate_ThrowsOnKeyedListCycle()
        => await Assert.That(() => JsonSchemaGenerator.Generate<IReadOnlyList<KeyedCycleSample>>(Options))
            .Throws<InvalidOperationException>();

    [Test]
    public async Task Generate_ThrowsOnRecursiveKeyedElement()
        => await Assert.That(() => JsonSchemaGenerator.Generate<IReadOnlyList<KeyedRecursiveSample>>(Options))
            .Throws<InvalidOperationException>();

    // A polymorphic member renders as an "anyOf" the exporter emits on its own; a recursive branch inside it
    // carries a "$ref" the guard must catch.
    [Test]
    public async Task Generate_ThrowsOnRecursivePolymorphicMember()
        => await Assert.That(() => JsonSchemaGenerator.Generate<IReadOnlyList<KeyedPolymorphicSample>>(Options))
            .Throws<InvalidOperationException>();

    // Not recursion: the exporter deduplicates the repeated inner member into a "$ref" pointer, which cannot
    // be embedded as a subschema any more than a recursive one.
    [Test]
    public async Task Generate_ThrowsOnRepeatedComplexMember()
        => await Assert.That(() => JsonSchemaGenerator.Generate<IReadOnlyList<KeyedRepeatedSample>>(Options))
            .Throws<InvalidOperationException>();

    // The generator refuses the models the converter refuses, through the shared resolve-and-validate step.
    [Test]
    public async Task Generate_ThrowsOnNonStringKeyProperty()
        => await Assert.That(() => JsonSchemaGenerator.Generate<IReadOnlyList<NonStringKeySample>>(Options))
            .Throws<InvalidOperationException>();

    [Test]
    public async Task Generate_ThrowsOnSameKeyAndValueJsonName()
        => await Assert.That(() => JsonSchemaGenerator.Generate<IReadOnlyList<SameKeyValueSample>>(Options))
            .Throws<InvalidOperationException>();

    private static JsonNode Generate() => JsonSchemaGenerator.Generate<GeneratorSample>(Options);

    private static JsonNode GenerateWithDefaults()
        => JsonSchemaGenerator.Generate<DefaultsSchemaSample>(Options, defaults: new DefaultsValuesSample());

    private static JsonNode GenerateKeyed() => JsonSchemaGenerator.Generate<KeyedSchemaSample>(Options);
}
