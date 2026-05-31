// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core.JsonSchema;

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

    private static JsonSourceMap Map(string json) => JsonSourceMap.Build(Encoding.UTF8.GetBytes(json));
}
