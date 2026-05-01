// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Buildvana.Core.Json;

partial class JsonBuildHostExtensions
{
    private static bool HasUtf8Bom(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static List<JsonValueEdit> CollectJsonStringEdits(ReadOnlySpan<byte> jsonSpan, int offsetInFile, JsonStringValueRewriter rewriter)
    {
        var reader = new Utf8JsonReader(
            jsonSpan,
            new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

        var edits = new List<JsonValueEdit>();
        var pathSegments = new List<string>();

        // Parallel stack: one entry per open container, true iff that container's start consumed
        // a property name (i.e. pushed onto pathSegments). Without this, EndObject/EndArray would
        // pop a segment for *every* close — including containers that are array elements, which
        // never push — corrupting the path for siblings that follow.
        var containerPushedSegment = new Stack<bool>();
        string? pendingProperty = null;
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    pendingProperty = reader.GetString();
                    break;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    if (pendingProperty is not null)
                    {
                        pathSegments.Add(pendingProperty);
                        containerPushedSegment.Push(true);
                        pendingProperty = null;
                    }
                    else
                    {
                        containerPushedSegment.Push(false);
                    }

                    break;

                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (containerPushedSegment.Pop())
                    {
                        pathSegments.RemoveAt(pathSegments.Count - 1);
                    }

                    break;

                case JsonTokenType.String:
                    // Only invoke the rewriter for string values that are direct properties of an object;
                    // string elements of arrays have no pending property name and are skipped on purpose.
                    if (pendingProperty is not null)
                    {
                        TryRecordStringEdit(ref reader, offsetInFile, rewriter, pathSegments, pendingProperty, edits);
                        pendingProperty = null;
                    }

                    break;

                default:
                    // Any other primitive value (Number / True / False / Null) consumes the pending name.
                    pendingProperty = null;
                    break;
            }
        }

        return edits;
    }

    private static void TryRecordStringEdit(
        ref Utf8JsonReader reader,
        int offsetInFile,
        JsonStringValueRewriter rewriter,
        List<string> pathSegments,
        string propertyName,
        List<JsonValueEdit> edits)
    {
        var currentValue = reader.GetString()!;
        pathSegments.Add(propertyName);
        var newValue = rewriter(pathSegments, currentValue);
        pathSegments.RemoveAt(pathSegments.Count - 1);

        if (newValue is null || string.Equals(newValue, currentValue, StringComparison.Ordinal))
        {
            return;
        }

        // TokenStartIndex points to the opening quote; ValueSpan covers the bytes
        // between the quotes (escape sequences included). The closing quote and any
        // surrounding whitespace are deliberately untouched.
        var innerStart = (int)reader.TokenStartIndex + 1 + offsetInFile;
        var innerLength = reader.ValueSpan.Length;
        var encoded = JsonEncodedText.Encode(newValue.AsSpan(), JavaScriptEncoder.UnsafeRelaxedJsonEscaping).EncodedUtf8Bytes.ToArray();
        edits.Add(new JsonValueEdit(innerStart, innerLength, encoded));
    }
}
