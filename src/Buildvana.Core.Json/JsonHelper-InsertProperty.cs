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

        byte[] originalBytes;
        try
        {
            originalBytes = File.ReadAllBytes(path);
        }
        catch (IOException e)
        {
            throw new BuildFailedException($"Could not read from {path}: {e.Message}", e);
        }

        var bomLength = HasUtf8Bom(originalBytes) ? 3 : 0;
        int braceIndex;
        int firstContentIndex;
        bool objectIsEmpty;
        try
        {
            var inserting = TryLocateInsertion(
                path,
                originalBytes.AsSpan(bomLength),
                bomLength,
                parentPath,
                propertyName,
                out braceIndex,
                out firstContentIndex,
                out objectIsEmpty);
            if (!inserting)
            {
                return false;
            }
        }
        catch (JsonException e)
        {
            throw new BuildFailedException($"{path} does not contain valid JSON.", e);
        }

        var newline = ContainsCrLf(originalBytes) ? "\r\n" : "\n";
        string insertionText;
        var replaceStart = braceIndex + 1;
        var replaceLength = 0;
        if (objectIsEmpty)
        {
            var baseIndent = LeadingWhitespaceOfLine(originalBytes, braceIndex);
            var innerIndent = baseIndent + "  ";
            var valueText = SerializeValue(value, newline, innerIndent);
            insertionText = $"{newline}{innerIndent}{EncodePropertyName(propertyName)}: {valueText}{newline}{baseIndent}";

            // Replace the whitespace between the braces; anything else (e.g. a comment) stays in place.
            if (IsJsonWhitespace(originalBytes, replaceStart, firstContentIndex))
            {
                replaceLength = firstContentIndex - replaceStart;
            }
        }
        else
        {
            // On a multi-line object, mimic the indentation of the first existing property; on a single-line
            // object, splice the new property right after the opening brace.
            var indent = IndentBeforeContent(originalBytes, replaceStart, firstContentIndex);
            var valueText = SerializeValue(value, newline, indent ?? string.Empty);
            insertionText = indent is null
                ? $"{EncodePropertyName(propertyName)}: {valueText}, "
                : $"{newline}{indent}{EncodePropertyName(propertyName)}: {valueText},";
        }

        var insertion = Encoding.UTF8.GetBytes(insertionText);
        using var output = new MemoryStream(originalBytes.Length + insertion.Length);
        output.Write(originalBytes, 0, replaceStart);
        output.Write(insertion, 0, insertion.Length);
        output.Write(originalBytes, replaceStart + replaceLength, originalBytes.Length - replaceStart - replaceLength);

        try
        {
            File.WriteAllBytes(path, output.ToArray());
        }
        catch (IOException e)
        {
            throw new BuildFailedException($"Could not write to {path}: {e.Message}", e);
        }

        return true;
    }

    // Finds the object at parentPath and the byte positions relevant to inserting into it (file coordinates):
    // its opening brace and its first content token (the closing brace, for an empty object). Returns false,
    // without positions, if the object already has a property named propertyName. Fails the build if the
    // document contains no object at parentPath; JsonException bubbles up on malformed JSON.
    private static bool TryLocateInsertion(
        string path,
        ReadOnlySpan<byte> jsonSpan,
        int offsetInFile,
        IReadOnlyList<string> parentPath,
        string propertyName,
        out int braceIndex,
        out int firstContentIndex,
        out bool objectIsEmpty)
    {
        var reader = new Utf8JsonReader(
            jsonSpan,
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
            offsetInFile,
            propertyName,
            out braceIndex,
            out firstContentIndex,
            out objectIsEmpty);
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
    // push no segment and would otherwise be confused with their containing array's property.
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
