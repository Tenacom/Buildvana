// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Buildvana.Core.Json;

internal sealed class JsonKeyedObjectConverterTests
{
    [Test]
    public async Task CanConvert_MatchesExactlyAttributedReadOnlyLists()
    {
        var converter = new JsonKeyedObjectConverter();
        await Assert.That(converter.CanConvert(typeof(IReadOnlyList<KeyedValueSample>))).IsTrue();
        await Assert.That(converter.CanConvert(typeof(List<KeyedValueSample>))).IsFalse();
        await Assert.That(converter.CanConvert(typeof(IReadOnlyList<UnkeyedSample>))).IsFalse();
    }

    [Test]
    public async Task Read_ValueShape_ReadsElementsInDocumentOrder()
    {
        var list = Deserialize<KeyedValueSample>("""{"Louis*":"minor","*":"latest"}""");
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0]).IsEqualTo(new KeyedValueSample { Pattern = "Louis*", Policy = "minor" });
        await Assert.That(list[1]).IsEqualTo(new KeyedValueSample { Pattern = "*", Policy = "latest" });
    }

    [Test]
    public async Task Write_ValueShape_WritesOneMemberPerElement()
    {
        var json = Serialize<KeyedValueSample>([
            new() { Pattern = "Louis*", Policy = "minor" },
            new() { Pattern = "*", Policy = "latest" }]);
        await Assert.That(json).IsEqualTo("""{"Louis*":"minor","*":"latest"}""");
    }

    [Test]
    public async Task Read_MembersShape_ReadsElementsInDocumentOrder()
    {
        var list = Deserialize<KeyedGroupSample>("""{"G1":{"files":"src/*.props","retries":3,"tags":["a","b"]},"G2":{}}""");
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0].Caption).IsEqualTo("G1");
        await Assert.That(list[0].Files).IsEqualTo("src/*.props");
        await Assert.That(list[0].Retries).IsEqualTo(3);
        await Assert.That(list[0].Tags!.SequenceEqual(["a", "b"])).IsTrue();
        await Assert.That(list[1].Caption).IsEqualTo("G2");
        await Assert.That(list[1].Files).IsNull();
    }

    [Test]
    public async Task Write_MembersShape_WritesRemainingMembers()
    {
        var json = Serialize<KeyedGroupSample>([new() { Caption = "G1", Files = "x", Retries = 2, Tags = ["a"] }]);
        await Assert.That(json).IsEqualTo("""{"G1":{"files":"x","retries":2,"tags":["a"]}}""");
    }

    [Test]
    public async Task Read_EmptyObject_ReturnsEmptyList()
    {
        var list = Deserialize<KeyedValueSample>("{}");
        await Assert.That(list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Read_Null_ReturnsNull()
    {
        var list = JsonSerializer.Deserialize<IReadOnlyList<KeyedValueSample>>("null", CreateOptions());
        await Assert.That(list).IsNull();
    }

    [Test]
    public async Task Read_NonObject_Throws()
    {
        static IReadOnlyList<KeyedValueSample> Act() => Deserialize<KeyedValueSample>("""["a"]""");

        var exception = await Assert.That(Act).Throws<JsonException>();
        await Assert.That(exception!.Message).Contains("Expected a JSON object");
    }

    [Test]
    public async Task Read_DuplicateKey_ByDefault_KeepsFirstPositionTakesLastValue()
    {
        var list = Deserialize<KeyedValueSample>("""{"a":"one","b":"two","a":"three"}""");
        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0]).IsEqualTo(new KeyedValueSample { Pattern = "a", Policy = "three" });
        await Assert.That(list[1]).IsEqualTo(new KeyedValueSample { Pattern = "b", Policy = "two" });
    }

    [Test]
    public async Task Read_DuplicateKey_WhenDisallowed_Throws()
    {
        var options = CreateOptions(allowDuplicateProperties: false);
        object? Act() => JsonSerializer.Deserialize<IReadOnlyList<KeyedValueSample>>("""{"a":"one","a":"two"}""", options);

        var exception = await Assert.That(Act).Throws<JsonException>();
        await Assert.That(exception!.Message).Contains("Duplicate key 'a'");
    }

    [Test]
    public async Task Read_DuplicateDetection_IsCaseSensitive()
    {
        var list = Deserialize<KeyedValueSample>("""{"a":"one","A":"two"}""");
        await Assert.That(list.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Write_DuplicateKey_Throws()
    {
        static string Act() => Serialize<KeyedValueSample>([
            new() { Pattern = "a", Policy = "one" },
            new() { Pattern = "a", Policy = "two" }]);

        var exception = await Assert.That(Act).Throws<JsonException>();
        await Assert.That(exception!.Message).Contains("Duplicate key 'a'");
    }

    [Test]
    public async Task Read_MembersShape_DuplicateInnerMember_TakesLastValue()
    {
        var list = Deserialize<KeyedGroupSample>("""{"G1":{"retries":1,"retries":2}}""");
        await Assert.That(list[0].Retries).IsEqualTo(2);
    }

    [Test]
    public async Task Read_MembersShape_DuplicateInnerMemberWhenDisallowed_Throws()
    {
        var options = CreateOptions(allowDuplicateProperties: false);
        object? Act() => JsonSerializer.Deserialize<IReadOnlyList<KeyedGroupSample>>("""{"G1":{"retries":1,"retries":2}}""", options);

        _ = await Assert.That(Act).Throws<JsonException>();
    }

    [Test]
    public async Task Read_MembersShape_KeyRestatedInsideValue_Throws()
    {
        static IReadOnlyList<KeyedGroupSample> Act() => Deserialize<KeyedGroupSample>("""{"G1":{"caption":"X"}}""");

        var exception = await Assert.That(Act).Throws<JsonException>();
        await Assert.That(exception!.Message).Contains("must not state the key property 'caption'");
    }

    [Test]
    public async Task Read_MembersShape_NonObjectValue_Throws()
    {
        static IReadOnlyList<KeyedGroupSample> Act() => Deserialize<KeyedGroupSample>("""{"G1":42}""");

        var exception = await Assert.That(Act).Throws<JsonException>();
        await Assert.That(exception!.Message).Contains("must be a JSON object");
    }

    [Test]
    public async Task Read_MembersShape_UnknownMemberWhenDisallowed_Throws()
    {
        var options = CreateOptions();
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        object? Act() => JsonSerializer.Deserialize<IReadOnlyList<KeyedGroupSample>>("""{"G1":{"bogus":1}}""", options);

        _ = await Assert.That(Act).Throws<JsonException>();
    }

    [Test]
    public async Task Read_RenamedKey_ResolvesJsonPropertyName()
    {
        var list = Deserialize<RenamedKeySample>("""{"k1":7}""");
        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(new RenamedKeySample { Id = "k1", Value = 7 });
    }

    [Test]
    public async Task Write_RenamedKey_ResolvesJsonPropertyName()
    {
        var json = Serialize<RenamedKeySample>([new() { Id = "k1", Value = 7 }]);
        await Assert.That(json).IsEqualTo("""{"k1":7}""");
    }

    [Test]
    public async Task Write_NullKey_Throws()
    {
        static string Act() => Serialize<KeyedValueSample>([new() { Pattern = null!, Policy = "x" }]);

        var exception = await Assert.That(Act).Throws<JsonException>();
        await Assert.That(exception!.Message).Contains("non-null string");
    }

    [Test]
    public async Task Write_ValueShape_NullValue_WritesNull()
    {
        var json = Serialize<KeyedValueSample>([new() { Pattern = "a", Policy = null! }]);
        await Assert.That(json).IsEqualTo("""{"a":null}""");
    }

    [Test]
    public async Task Read_ValueShape_NullValue_ReadsNullValueProperty()
    {
        var list = Deserialize<KeyedValueSample>("""{"a":null}""");
        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0].Policy).IsNull();
    }

    [Test]
    public async Task ReadWrite_SourceGeneratedContext_ResolvesGeneratedNames()
    {
        // Default.Options is read-only, so the copy constructor is how a converter joins a generated context.
        // The copy carries the context's naming policy and unmapped-member handling; the resolver is the context,
        // so elements go through generated metadata, not reflection.
        var options = new JsonSerializerOptions(KeyedSampleJsonContext.Default.Options)
        {
            Converters = { new JsonKeyedObjectConverter() },
        };

        var list = JsonSerializer.Deserialize<IReadOnlyList<KeyedGroupSample>>("""{"G1":{"files":"x"}}""", options);
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].Caption).IsEqualTo("G1");
        await Assert.That(list[0].Files).IsEqualTo("x");

        var json = JsonSerializer.Serialize(list, options);
        await Assert.That(json).IsEqualTo("""{"G1":{"files":"x","retries":0,"tags":null}}""");
    }

    [Test]
    public async Task ReadWrite_CustomizedContract_FollowsTypeInfoNames()
    {
        // A contract modifier renames the key property's JSON name. The serializer's metadata is then the only
        // source that knows the effective name; recomputing it from the naming policy would synthesize a document
        // the element deserializer rejects.
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            if (typeInfo.Type == typeof(KeyedGroupSample))
            {
                foreach (var property in typeInfo.Properties)
                {
                    if (property.Name == "caption")
                    {
                        property.Name = "header";
                    }
                }
            }
        });
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonKeyedObjectConverter() },
        };

        var list = JsonSerializer.Deserialize<IReadOnlyList<KeyedGroupSample>>("""{"G1":{"files":"x"}}""", options);
        await Assert.That(list![0].Caption).IsEqualTo("G1");
        await Assert.That(list[0].Files).IsEqualTo("x");

        var json = JsonSerializer.Serialize(list, options);
        await Assert.That(json).IsEqualTo("""{"G1":{"files":"x","retries":0,"tags":null}}""");
    }

    [Test]
    public async Task Read_MissingKeyProperty_Throws()
    {
        static object Act() => Deserialize<MissingKeySample>("{}");

        var exception = await Assert.That(Act).Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("no serializable property 'Nope'");
    }

    [Test]
    public async Task Read_MissingValueProperty_Throws()
    {
        static object Act() => Deserialize<MissingValueSample>("{}");

        var exception = await Assert.That(Act).Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("no serializable property 'Nope'");
    }

    [Test]
    public async Task Read_NonStringKeyProperty_Throws()
    {
        static object Act() => Deserialize<NonStringKeySample>("{}");

        var exception = await Assert.That(Act).Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("must be of type string");
    }

    [Test]
    public async Task Read_SameKeyAndValueProperty_Throws()
    {
        static object Act() => Deserialize<SameKeyValueSample>("{}");

        var exception = await Assert.That(Act).Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("to the same JSON name 'name'");
    }

    private static JsonSerializerOptions CreateOptions(bool allowDuplicateProperties = true)
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowDuplicateProperties = allowDuplicateProperties,
            Converters = { new JsonKeyedObjectConverter() },
        };

    private static IReadOnlyList<T> Deserialize<T>(string json)
        => JsonSerializer.Deserialize<IReadOnlyList<T>>(json, CreateOptions())!;

    private static string Serialize<T>(IReadOnlyList<T> list)
        => JsonSerializer.Serialize(list, CreateOptions());
}
