// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Buildvana.Core.Json.Schema;

/// <summary>
/// Maps RFC 6901 JSON Pointers to 1-based line and column positions within a UTF-8 JSON document, so that
/// validation errors (which are keyed by pointer) can be reported at their location in the source.
/// </summary>
/// <remarks>
/// <para>Columns are counted in characters (UTF-16 code units), not bytes, so positions stay correct for
/// documents containing non-ASCII text.</para>
/// <para>A pointer occurs twice only when an object repeats a member name, array elements being numbered and
/// the root occurring once. The first occurrence is the one the map answers with, and every repeat is
/// reported by <see cref="DuplicateMembers"/>, for callers that must refuse a document
/// <see cref="System.Text.Json.Nodes.JsonObject"/> cannot represent.</para>
/// </remarks>
public sealed partial class JsonSourceMap
{
    private readonly Dictionary<string, (int Line, int Column)> _positions;
    private readonly Dictionary<string, (int Line, int Column)> _namePositions;

    private JsonSourceMap(
        Dictionary<string, (int Line, int Column)> positions,
        Dictionary<string, (int Line, int Column)> namePositions,
        IReadOnlyList<JsonDuplicateMember> duplicateMembers)
    {
        _positions = positions;
        _namePositions = namePositions;
        DuplicateMembers = duplicateMembers;
    }

    /// <summary>
    /// Gets the repeats of object member names found in the document, in document order. Empty when every
    /// object states each of its member names once.
    /// </summary>
    public IReadOnlyList<JsonDuplicateMember> DuplicateMembers { get; }

    /// <summary>
    /// Builds a source map from a UTF-8 encoded JSON document.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 bytes of the JSON document.</param>
    /// <returns>A <see cref="JsonSourceMap"/> describing <paramref name="utf8Json"/>.</returns>
    /// <exception cref="JsonException"><paramref name="utf8Json"/> is not valid JSON.</exception>
    public static JsonSourceMap Build(ReadOnlySpan<byte> utf8Json)
    {
        var positions = new Dictionary<string, (int Line, int Column)>(StringComparer.Ordinal);
        var namePositions = new Dictionary<string, (int Line, int Column)>(StringComparer.Ordinal);
        var duplicates = new List<JsonDuplicateMember>();
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
            var tokenStart = reader.TokenStartIndex;
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    var frame = frames.Peek();
                    frame.PendingKey = reader.GetString();
                    frame.PendingKeyStart = tokenStart;
                    break;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    var (containerPointer, containerNameStart) = NextPointer(frames);
                    Record(positions, namePositions, duplicates, containerPointer, tokenStart, containerNameStart, lineStarts, utf8Json);
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
                    var (pointer, nameStart) = NextPointer(frames);
                    Record(positions, namePositions, duplicates, pointer, tokenStart, nameStart, lineStarts, utf8Json);
                    break;
            }
        }

        return new JsonSourceMap(positions, namePositions, duplicates);
    }

    /// <summary>
    /// Gets the source position of the value at the specified JSON Pointer.
    /// </summary>
    /// <param name="jsonPointer">An RFC 6901 JSON Pointer (an empty string for the document root).</param>
    /// <param name="line">When this method returns <see langword="true"/>, the 1-based line number.</param>
    /// <param name="column">When this method returns <see langword="true"/>, the 1-based column number.</param>
    /// <returns>
    /// <see langword="true"/> if a position was recorded for <paramref name="jsonPointer"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
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

    /// <summary>
    /// Gets the source position of the name of the object member at the specified JSON Pointer.
    /// </summary>
    /// <param name="jsonPointer">An RFC 6901 JSON Pointer naming an object member.</param>
    /// <param name="line">When this method returns <see langword="true"/>, the 1-based line number.</param>
    /// <param name="column">When this method returns <see langword="true"/>, the 1-based column number.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="jsonPointer"/> names an object member; otherwise,
    /// <see langword="false"/>, an array element and the root having no name of their own.
    /// </returns>
    /// <remarks>
    /// <para>The position is that of the name's opening quote, which is where a diagnostic about the name
    /// itself belongs: a blank member name is not made blank by the value it introduces.</para>
    /// <para>A repeated member name answers with its first occurrence, as <see cref="TryGetPosition"/> does;
    /// every repeat carries its own position in <see cref="DuplicateMembers"/>.</para>
    /// </remarks>
    public bool TryGetNamePosition(string jsonPointer, out int line, out int column)
    {
        if (_namePositions.TryGetValue(jsonPointer, out var position))
        {
            (line, column) = position;
            return true;
        }

        (line, column) = (0, 0);
        return false;
    }

    // Returns the pointer of the value the reader is on, together with the offset of the member name that
    // introduces it: where an error about the name — a repeat, or a propertyNames failure — is reported,
    // rather than at the value the name introduces. An array element and the root have no name of their own.
    private static (string Pointer, long? NameStart) NextPointer(Stack<Frame> frames)
    {
        if (frames.Count == 0)
        {
            return (string.Empty, null);
        }

        var top = frames.Peek();
        if (top.IsArray)
        {
            var childPointer = $"{top.Pointer}/{top.NextIndex.ToString(CultureInfo.InvariantCulture)}";
            top.NextIndex++;
            return (childPointer, null);
        }

        var key = top.PendingKey ?? string.Empty;
        var keyStart = top.PendingKeyStart;
        top.PendingKey = null;
        return ($"{top.Pointer}/{Escape(key)}", keyStart);
    }

    private static string Escape(string token)
        => token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    // The first occurrence is the one the map answers with: at the position of its value, which is what a
    // schema error is about, and at the position of its name, which is what an error about the name itself is
    // about. A repeat can only be a duplicate object member, since array elements are numbered and the root
    // occurs once, so it is recorded for the caller to report — at its own name, which is the part a reader
    // has to delete or merge.
    private static void Record(
        Dictionary<string, (int Line, int Column)> positions,
        Dictionary<string, (int Line, int Column)> namePositions,
        List<JsonDuplicateMember> duplicates,
        string pointer,
        long tokenStart,
        long? nameStart,
        List<int> lineStarts,
        ReadOnlySpan<byte> utf8Json)
    {
        var recorded = positions.TryAdd(pointer, OffsetToPosition((int)tokenStart, lineStarts, utf8Json));

        // An array element and the root have no name of their own, and cannot repeat either.
        if (nameStart is not { } nameOffset)
        {
            return;
        }

        var namePosition = OffsetToPosition((int)nameOffset, lineStarts, utf8Json);
        if (recorded)
        {
            namePositions.Add(pointer, namePosition);
        }
        else
        {
            duplicates.Add(new JsonDuplicateMember(NameOf(pointer), pointer, namePosition.Line, namePosition.Column));
        }
    }

    // The member name is the pointer's last token, with RFC 6901 escaping undone: "~1" back to "/", then
    // "~0" back to "~", in that order, so an escaped "~1" survives the round trip.
    private static string NameOf(string pointer)
        => pointer[(pointer.LastIndexOf('/') + 1)..]
            .Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);

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

    private static (int Line, int Column) OffsetToPosition(
        int offset,
        List<int> lineStarts,
        ReadOnlySpan<byte> utf8Json)
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
