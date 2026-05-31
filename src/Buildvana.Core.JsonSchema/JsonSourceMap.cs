// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Maps RFC 6901 JSON Pointers to 1-based line and column positions within a UTF-8 JSON document, so that
/// validation errors (which are keyed by pointer) can be reported at their location in the source.
/// </summary>
/// <remarks>
/// <para>Columns are counted in characters (UTF-16 code units), not bytes, so positions stay correct for
/// documents containing non-ASCII text.</para>
/// </remarks>
public sealed partial class JsonSourceMap
{
    private readonly Dictionary<string, (int Line, int Column)> _positions;

    private JsonSourceMap(Dictionary<string, (int Line, int Column)> positions)
    {
        _positions = positions;
    }

    /// <summary>
    /// Builds a source map from a UTF-8 encoded JSON document.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 bytes of the JSON document.</param>
    /// <returns>A <see cref="JsonSourceMap"/> describing <paramref name="utf8Json"/>.</returns>
    /// <exception cref="JsonException"><paramref name="utf8Json"/> is not valid JSON.</exception>
    public static JsonSourceMap Build(ReadOnlySpan<byte> utf8Json)
    {
        var positions = new Dictionary<string, (int Line, int Column)>(StringComparer.Ordinal);
        var lineStarts = BuildLineStarts(utf8Json);
        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

        var frames = new Stack<Frame>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    frames.Peek().PendingKey = reader.GetString();
                    break;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    var containerPointer = NextPointer(frames);
                    Record(positions, containerPointer, reader.TokenStartIndex, lineStarts, utf8Json);
                    frames.Push(new Frame(containerPointer, reader.TokenType is JsonTokenType.StartArray));
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    _ = frames.Pop();
                    break;
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    Record(positions, NextPointer(frames), reader.TokenStartIndex, lineStarts, utf8Json);
                    break;
            }
        }

        return new JsonSourceMap(positions);
    }

    /// <summary>
    /// Gets the source position of the value at the specified JSON Pointer.
    /// </summary>
    /// <param name="jsonPointer">An RFC 6901 JSON Pointer (an empty string for the document root).</param>
    /// <param name="line">When this method returns <see langword="true"/>, the 1-based line number.</param>
    /// <param name="column">When this method returns <see langword="true"/>, the 1-based column number.</param>
    /// <returns><see langword="true"/> if a position was recorded for <paramref name="jsonPointer"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetPosition(string jsonPointer, out int line, out int column)
    {
        if (_positions.TryGetValue(jsonPointer, out var position))
        {
            (line, column) = position;
            return true;
        }

        (line, column) = (0, 0);
        return false;
    }

    private static string NextPointer(Stack<Frame> frames)
    {
        if (frames.Count == 0)
        {
            return string.Empty;
        }

        var top = frames.Peek();
        if (top.IsArray)
        {
            var childPointer = $"{top.Pointer}/{top.NextIndex.ToString(CultureInfo.InvariantCulture)}";
            top.NextIndex++;
            return childPointer;
        }

        var key = top.PendingKey ?? string.Empty;
        top.PendingKey = null;
        return $"{top.Pointer}/{Escape(key)}";
    }

    private static string Escape(string token)
        => token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static void Record(
        Dictionary<string, (int Line, int Column)> positions,
        string pointer,
        long tokenStartIndex,
        List<int> lineStarts,
        ReadOnlySpan<byte> utf8Json)
    {
        // The first occurrence wins; a well-formed document never repeats a pointer anyway.
        if (!positions.ContainsKey(pointer))
        {
            positions[pointer] = OffsetToPosition((int)tokenStartIndex, lineStarts, utf8Json);
        }
    }

    private static List<int> BuildLineStarts(ReadOnlySpan<byte> utf8Json)
    {
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < utf8Json.Length; i++)
        {
            if (utf8Json[i] is (byte)'\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        return lineStarts;
    }

    private static (int Line, int Column) OffsetToPosition(int offset, List<int> lineStarts, ReadOnlySpan<byte> utf8Json)
    {
        var lineIndex = FindLineIndex(lineStarts, offset);
        var lineStart = lineStarts[lineIndex];
        var column = Encoding.UTF8.GetCharCount(utf8Json[lineStart..offset]) + 1;
        return (lineIndex + 1, column);
    }

    private static int FindLineIndex(List<int> lineStarts, int offset)
    {
        // Binary search for the greatest line start that is less than or equal to offset.
        var low = 0;
        var high = lineStarts.Count - 1;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (lineStarts[mid] <= offset)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }
}
