// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Json;

public sealed partial class JsonHelper
{
    /// <inheritdoc cref="IJsonHelper.InsertProperty"/>
    public bool InsertProperty(string path, IReadOnlyList<string> parentPath, string propertyName, JsonNode value)
    {
        Guard.IsNotNullOrEmpty(path);
        Guard.IsNotNull(parentPath);
        Guard.IsNotNullOrEmpty(propertyName);
        Guard.IsNotNull(value);

        var originalBytes = ReadFileBytes(path);
        var inserting = TryLocateInsertion(
            path,
            originalBytes,
            parentPath,
            propertyName,
            out var braceIndex,
            out var firstContentIndex,
            out var objectIsEmpty);
        if (!inserting)
        {
            return false;
        }

        var (insertionText, replaceLength) = objectIsEmpty
            ? ComposeInsertionForEmptyObject(originalBytes, braceIndex, firstContentIndex, propertyName, value)
            : ComposeInsertionBeforeFirstProperty(originalBytes, braceIndex, firstContentIndex, propertyName, value);
        var newBytes = Splice(originalBytes, braceIndex + 1, replaceLength, insertionText);
        WriteFileBytes(path, newBytes);
        return true;
    }

    // Finds the object at parentPath and the byte positions relevant to inserting into it (file coordinates):
    // its opening brace and its first content token (the closing brace, for an empty object). Returns false,
    // without positions, if the object already has a property named propertyName. Fails the build if the
    // document is not valid JSON, or contains no object at parentPath.
    private static bool TryLocateInsertion(
        string path,
        byte[] originalBytes,
        IReadOnlyList<string> parentPath,
        string propertyName,
        out int braceIndex,
        out int firstContentIndex,
        out bool objectIsEmpty)
    {
        // Utf8JsonReader rejects a leading UTF-8 BOM; skip it for parsing, but keep positions in file coordinates.
        var bomLength = HasUtf8Bom(originalBytes) ? 3 : 0;
        try
        {
            var reader = new Utf8JsonReader(
                originalBytes.AsSpan(bomLength),
                new JsonReaderOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

            if (!TryFindParentObject(ref reader, parentPath))
            {
                var description = parentPath.Count == 0
                    ? "a root object"
                    : $"an object at '{string.Join('.', parentPath)}'";
                throw new BuildFailedException($"{path} does not contain {description}.");
            }

            return TryGetInsertionPoints(
                ref reader,
                bomLength,
                propertyName,
                out braceIndex,
                out firstContentIndex,
                out objectIsEmpty);
        }
        catch (JsonException e)
        {
            throw new BuildFailedException($"{path} does not contain valid JSON.", e);
        }
    }

    // Walks the document until it enters the object at parentPath (the root object, if parentPath is
    // empty), leaving the reader positioned on its opening brace. Returns false if the document contains
    // no such object. Same path-tracking technique as CollectJsonStringEdits; see there for the
    // containerPushedSegment rationale.
    private static bool TryFindParentObject(ref Utf8JsonReader reader, IReadOnlyList<string> parentPath)
    {
        var pathSegments = new List<string>();
        var containerPushedSegment = new Stack<bool>();
        string? pendingProperty = null;
        var found = false;
        while (!found && reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    pendingProperty = reader.GetString();
                    break;

                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    var pushed = pendingProperty is not null;
                    if (pushed)
                    {
                        pathSegments.Add(pendingProperty!);
                        pendingProperty = null;
                    }

                    containerPushedSegment.Push(pushed);
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        found = IsParentObject(parentPath, pathSegments, containerPushedSegment.Count, pushed);
                    }

                    break;

                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    if (containerPushedSegment.Pop())
                    {
                        pathSegments.RemoveAt(pathSegments.Count - 1);
                    }

                    break;

                default:
                    pendingProperty = null;
                    break;
            }
        }

        return found;
    }

    // The object just entered is the parent object only if its own property name completes parentPath
    // (or it is the root object and parentPath is empty): array-element objects never match, as they
    // push no segment and would otherwise be confused with their containing array's property. Their
    // descendants can still match, though: since elements push no segment, a property inside one
    // continues its array's path (see the interface doc's caveat about paths that traverse arrays).
    private static bool IsParentObject(
        IReadOnlyList<string> parentPath,
        IReadOnlyList<string> pathSegments,
        int containerDepth,
        bool pushedSegment)
    {
        var isRoot = parentPath.Count == 0 && containerDepth == 1;
        return isRoot || (pushedSegment && pathSegments.SequenceEqual(parentPath, StringComparer.Ordinal));
    }

    // Reports the byte positions relevant to inserting into the object whose opening brace the reader
    // is positioned on (file coordinates): the brace itself and the object's first content token (the
    // closing brace, for an empty object). Returns false if the object already has a property named
    // propertyName.
    private static bool TryGetInsertionPoints(
        ref Utf8JsonReader reader,
        int offsetInFile,
        string propertyName,
        out int braceIndex,
        out int firstContentIndex,
        out bool objectIsEmpty)
    {
        braceIndex = (int)reader.TokenStartIndex + offsetInFile;
        firstContentIndex = -1;
        objectIsEmpty = false;

        var nesting = 0;
        while (reader.Read())
        {
            if (firstContentIndex < 0)
            {
                firstContentIndex = (int)reader.TokenStartIndex + offsetInFile;
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    objectIsEmpty = true;
                    return true;
                }
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    nesting++;
                    break;

                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    if (nesting == 0)
                    {
                        return true;
                    }

                    nesting--;
                    break;

                case JsonTokenType.PropertyName when nesting == 0 && reader.ValueTextEquals(propertyName):
                    return false;
            }
        }

        // Unreachable: on truncated JSON, reader.Read() throws before running out of tokens.
        throw new JsonException($"Unexpected end of JSON while scanning the object at index {braceIndex}.");
    }

    // The insertion for an empty object rewrites it in multi-line form: the new property sits on its own
    // line, indented one level deeper than the line holding the opening brace, and the closing brace moves
    // to its own line at that line's indentation.
    private static (string Text, int ReplaceLength) ComposeInsertionForEmptyObject(
        byte[] originalBytes,
        int braceIndex,
        int firstContentIndex,
        string propertyName,
        JsonNode value)
    {
        var newline = ContainsCrLf(originalBytes) ? "\r\n" : "\n";
        var baseIndent = LeadingWhitespaceOfLine(originalBytes, braceIndex);
        var innerIndent = baseIndent + "  ";
        var valueText = SerializeValue(value, newline, innerIndent);
        var text = $"{newline}{innerIndent}{EncodePropertyName(propertyName)}: {valueText}{newline}{baseIndent}";

        // Replace the whitespace between the braces; anything else (e.g. a comment) stays in place.
        var replaceStart = braceIndex + 1;
        var replaceLength = IsJsonWhitespace(originalBytes, replaceStart, firstContentIndex)
            ? firstContentIndex - replaceStart
            : 0;
        return (text, replaceLength);
    }

    // On a multi-line object, the new property goes on its own line before the first existing property,
    // mimicking its indentation; on a single-line object, it is spliced right after the opening brace.
    // Nothing is replaced in either case.
    private static (string Text, int ReplaceLength) ComposeInsertionBeforeFirstProperty(
        byte[] originalBytes,
        int braceIndex,
        int firstContentIndex,
        string propertyName,
        JsonNode value)
    {
        var newline = ContainsCrLf(originalBytes) ? "\r\n" : "\n";
        var indent = IndentBeforeContent(originalBytes, braceIndex + 1, firstContentIndex);
        var valueText = SerializeValue(value, newline, indent ?? string.Empty);
        var text = indent is null
            ? $"{EncodePropertyName(propertyName)}: {valueText}, "
            : $"{newline}{indent}{EncodePropertyName(propertyName)}: {valueText},";
        return (text, 0);
    }

    // A copy of bytes with the replaceLength bytes at replaceStart replaced by the UTF-8 encoding of text.
    private static byte[] Splice(byte[] bytes, int replaceStart, int replaceLength, string text)
    {
        var insertion = Encoding.UTF8.GetBytes(text);
        using var output = new MemoryStream(bytes.Length + insertion.Length);
        output.Write(bytes, 0, replaceStart);
        output.Write(insertion, 0, insertion.Length);
        output.Write(bytes, replaceStart + replaceLength, bytes.Length - replaceStart - replaceLength);
        return output.ToArray();
    }

    private static string SerializeValue(JsonNode value, string newline, string indent)
    {
        var text = value.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            NewLine = newline,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        // Escape sequences keep string values free of raw newline characters, so this only touches
        // the structural newlines of multi-line (object or array) values.
        return text.Replace(newline, newline + indent, StringComparison.Ordinal);
    }

    private static string EncodePropertyName(string propertyName)
        => $"\"{JsonEncodedText.Encode(propertyName, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\"";

    private static bool ContainsCrLf(byte[] bytes)
    {
        for (var i = 1; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n' && bytes[i - 1] == (byte)'\r')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsJsonWhitespace(byte[] bytes, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (bytes[i] is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return false;
            }
        }

        return true;
    }

    // The whitespace prefix of the line containing the byte at index (e.g. the indentation of the line
    // holding an object's opening brace).
    private static string LeadingWhitespaceOfLine(byte[] bytes, int index)
    {
        var lineStart = index;
        while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n')
        {
            lineStart--;
        }

        return LeadingWhitespace(bytes, lineStart, index);
    }

    // The indentation in effect at the first content token of an object: the whitespace prefix of that
    // token's line, or null if the token is on the same line as the opening brace.
    private static string? IndentBeforeContent(byte[] bytes, int afterBrace, int contentIndex)
    {
        var lineStart = -1;
        for (var i = contentIndex - 1; i >= afterBrace; i--)
        {
            if (bytes[i] == (byte)'\n')
            {
                lineStart = i + 1;
                break;
            }
        }

        return lineStart < 0 ? null : LeadingWhitespace(bytes, lineStart, contentIndex);
    }

    private static string LeadingWhitespace(byte[] bytes, int start, int end)
    {
        var length = 0;
        while (start + length < end && bytes[start + length] is (byte)' ' or (byte)'\t')
        {
            length++;
        }

        return Encoding.UTF8.GetString(bytes, start, length);
    }
}
