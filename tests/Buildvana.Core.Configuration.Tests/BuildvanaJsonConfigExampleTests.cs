// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Buildvana.Core.Configuration;

// The two pure helpers behind the generated example. Neither runs on the model as it stands — every
// description fits one line, and every reference-bearing property carries its own example — so the schema
// walk exercises neither. They are tested here directly, on inputs of their own.
internal sealed class BuildvanaJsonConfigExampleTests
{
    // A description at the single-line limit is carried whole, however close it comes to the wrapped limit.
    [Test]
    public async Task WrapDescription_AtTheSingleLineLimit_ReturnsOneLine()
    {
        var description = Words(16) + "s";

        var lines = BuildvanaJsonConfigExample.WrapDescription(description);

        await Assert.That(description.Length).IsEqualTo(80);
        await Assert.That(lines.Count).IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo(description);
    }

    // Past the single-line limit the narrower one governs, so a second line never carries a word or two.
    [Test]
    public async Task WrapDescription_PastTheSingleLineLimit_WrapsAtTheNarrowerLimit()
    {
        var lines = BuildvanaJsonConfigExample.WrapDescription(Words(17));

        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines.All(static line => line.Length <= 72)).IsTrue();
    }

    // Wrapping moves the line breaks and nothing else: no word is dropped, split, or reordered.
    [Test]
    public async Task WrapDescription_PreservesTheText()
    {
        var description = Words(17);

        var lines = BuildvanaJsonConfigExample.WrapDescription(description);

        await Assert.That(string.Join(' ', lines)).IsEqualTo(description);
    }

    // The wrap reports the lines it needs, however many. Refusing a third one is the caller's decision,
    // which it cannot make on a count that stops at two.
    [Test]
    public async Task WrapDescription_LongText_ReportsEveryLineItNeeds()
        => await Assert.That(BuildvanaJsonConfigExample.WrapDescription(Words(35)).Count).IsEqualTo(3);

    [Test]
    public async Task ResolveReference_FollowsADocumentLocalPointer()
    {
        var root = SampleSchema();

        var resolved = BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/args"), root, "args");

        await Assert.That(resolved["type"]!.GetValue<string>()).IsEqualTo("array");
    }

    // A member name holding a slash or a tilde is escaped in a pointer, and has to survive the trip back.
    [Test]
    public async Task ResolveReference_UnescapesPointerTokens()
    {
        var root = SampleSchema();

        var slash = BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/a~1b"), root, "a/b");
        var tilde = BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/c~0d"), root, "c~d");

        await Assert.That(slash["type"]!.GetValue<string>()).IsEqualTo("string");
        await Assert.That(tilde["type"]!.GetValue<string>()).IsEqualTo("boolean");
    }

    [Test]
    public async Task ResolveReference_UnresolvablePointer_Throws()
    {
        var root = SampleSchema();

        await Assert.That(() => BuildvanaJsonConfigExample.ResolveReference(Reference("#/properties/nope"), root, "nope"))
            .Throws<InvalidOperationException>();
    }

    // Four-letter words joined by single spaces, so the text is (5 * count - 1) characters long and a greedy
    // wrap has somewhere to break.
    private static string Words(int count) => string.Join(' ', Enumerable.Repeat("word", count));

    private static JsonObject Reference(string pointer) => new() { ["$ref"] = pointer };

    private static JsonObject SampleSchema() => new()
    {
        ["properties"] = new JsonObject
        {
            ["args"] = new JsonObject { ["type"] = "array" },
            ["a/b"] = new JsonObject { ["type"] = "string" },
            ["c~d"] = new JsonObject { ["type"] = "boolean" },
        },
    };
}
