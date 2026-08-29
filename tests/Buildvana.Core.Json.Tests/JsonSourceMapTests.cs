// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core.Json.Schema;

internal sealed class JsonSourceMapTests
{
    [Test]
    public async Task TryGetPosition_NestedValue_ReturnsLineAndColumn()
    {
        var map = Map("{\n  \"a\": {\n    \"b\": 1\n  }\n}");
        var found = map.TryGetPosition("/a/b", out var line, out var column);
        await Assert.That(found).IsTrue();
        await Assert.That(line).IsEqualTo(3);
        await Assert.That(column).IsEqualTo(10);
    }

    [Test]
    public async Task TryGetPosition_ObjectValue_PointsAtOpeningBrace()
    {
        var map = Map("{\n  \"a\": {\n    \"b\": 1\n  }\n}");
        map.TryGetPosition("/a", out var line, out var column);
        await Assert.That(line).IsEqualTo(2);
        await Assert.That(column).IsEqualTo(8);
    }

    [Test]
    public async Task TryGetPosition_ArrayElement_UsesIndexPointer()
    {
        var map = Map("[\n  10,\n  20\n]");
        map.TryGetPosition("/1", out var line, out var column);
        await Assert.That(line).IsEqualTo(3);
        await Assert.That(column).IsEqualTo(3);
    }

    [Test]
    public async Task TryGetPosition_NonAsciiKey_CountsCharactersNotBytes()
    {
        // "ä" is two UTF-8 bytes but one character; a byte-based column would place "b" one too far.
        var map = Map("{\"ä\":1,\"b\":2}");
        map.TryGetPosition("/b", out var line, out var column);
        await Assert.That(line).IsEqualTo(1);
        await Assert.That(column).IsEqualTo(12);
    }

    [Test]
    public async Task TryGetPosition_UnknownPointer_ReturnsFalse()
    {
        var map = Map("{\"a\":1}");
        var found = map.TryGetPosition("/nope", out _, out _);
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task DuplicateMembers_DocumentWithoutRepeats_IsEmpty()
    {
        var map = Map("{\"a\":1,\"b\":{\"a\":2},\"c\":[1,2]}");
        await Assert.That(map.DuplicateMembers.Count).IsEqualTo(0);
    }

    // The map keeps answering with the first occurrence, at its value; the repeat is what gets reported, and
    // it is reported at its name, which is the part a reader has to delete.
    [Test]
    public async Task DuplicateMembers_RepeatedMember_ReportsTheRepeatAndKeepsTheFirstPosition()
    {
        var map = Map("{\n  \"a\": 1,\n  \"b\": 2,\n  \"a\": 3\n}");

        await Assert.That(map.DuplicateMembers.Count).IsEqualTo(1);
        await Assert.That(map.DuplicateMembers[0].Name).IsEqualTo("a");
        await Assert.That(map.DuplicateMembers[0].JsonPointer).IsEqualTo("/a");
        await Assert.That(map.DuplicateMembers[0].Line).IsEqualTo(4);
        await Assert.That(map.DuplicateMembers[0].Column).IsEqualTo(3);

        map.TryGetPosition("/a", out var line, out var column);
        await Assert.That(line).IsEqualTo(2);
        await Assert.That(column).IsEqualTo(8);
    }

    // A repeated member whose value is an object or an array is caught at its opening token, like any other,
    // and reported at its name all the same.
    [Test]
    public async Task DuplicateMembers_RepeatedContainerMember_IsReported()
    {
        var map = Map("{\"a\":{\"x\":1},\"a\":[1]}");

        await Assert.That(map.DuplicateMembers.Count).IsEqualTo(1);
        await Assert.That(map.DuplicateMembers[0].Name).IsEqualTo("a");
        await Assert.That(map.DuplicateMembers[0].Column).IsEqualTo(14);
    }

    // Every repeat is reported, in document order, so one run can name them all.
    [Test]
    public async Task DuplicateMembers_SeveralRepeats_AreReportedInDocumentOrder()
    {
        var map = Map("{\"a\":1,\"a\":2,\"a\":3,\"b\":{\"c\":1,\"c\":2}}");

        await Assert.That(map.DuplicateMembers.Count).IsEqualTo(3);
        await Assert.That(map.DuplicateMembers[0].JsonPointer).IsEqualTo("/a");
        await Assert.That(map.DuplicateMembers[1].JsonPointer).IsEqualTo("/a");
        await Assert.That(map.DuplicateMembers[2].JsonPointer).IsEqualTo("/b/c");
    }

    // The name is read back from the pointer, so the RFC 6901 escaping the pointer carries has to be undone.
    [Test]
    public async Task DuplicateMembers_NameNeedingEscaping_IsReportedUnescaped()
    {
        var map = Map("{\"a/b~c\":1,\"a/b~c\":2}");

        await Assert.That(map.DuplicateMembers.Count).IsEqualTo(1);
        await Assert.That(map.DuplicateMembers[0].Name).IsEqualTo("a/b~c");
    }

    private static JsonSourceMap Map(string json) => JsonSourceMap.Build(Encoding.UTF8.GetBytes(json));
}
