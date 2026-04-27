// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cake.Core;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;
using SysFile = System.IO.File;

namespace Buildvana.Tool.Utilities;

partial class CakeContextExtensions
{
    /// <summary>
    /// Parses a JSON object from a string. Fails the build if not successful.
    /// </summary>
    /// <param name="this">The Cake context.</param>
    /// <param name="str">The string to parse.</param>
    /// <param name="description">A description of the string for exception messages.</param>
    /// <returns>The parsed object.</returns>
    public static JsonObject ParseJsonObject(this ICakeContext @this, string str, string description = "The provided string")
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(
                str,
                new JsonNodeOptions { PropertyNameCaseInsensitive = false },
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
        }
        catch (JsonException)
        {
            return @this.Fail<JsonObject>($"{description} is not valid JSON.");
        }

        return node switch {
            null => @this.Fail<JsonObject>($"{description} was parsed as JSON null."),
            JsonObject obj => obj,
            object other => @this.Fail<JsonObject>($"{description} was parsed as a {other.GetType().Name}, not a {nameof(JsonObject)}."),
        };
    }

    /// <summary>
    /// Loads a JSON object from a file. Fails the build if not successful.
    /// </summary>
    /// <param name="this">The Cake context.</param>
    /// <param name="path">The path of the file to parse.</param>
    /// <returns>The parsed object.</returns>
    public static JsonObject LoadJsonObject(this ICakeContext @this, FilePath path)
    {
        Guard.IsNotNull(path);

        var fullPath = path.FullPath;
        JsonNode? node;
        try
        {
            using var stream = SysFile.OpenRead(fullPath);
            node = JsonNode.Parse(
                stream,
                new JsonNodeOptions { PropertyNameCaseInsensitive = false },
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
        }
        catch (IOException e)
        {
            return @this.Fail<JsonObject>($"Could not read from {fullPath}: {e.Message}");
        }
        catch (JsonException)
        {
            return @this.Fail<JsonObject>($"{fullPath} does not contain valid JSON.");
        }

        return node switch {
            null => @this.Fail<JsonObject>($"{fullPath} was parsed as JSON null."),
            JsonObject obj => obj,
            object other => @this.Fail<JsonObject>($"{fullPath} was parsed as a {other.GetType().Name}, not a {nameof(JsonObject)}."),
        };
    }

    /// <summary>
    /// Saves a JSON object to a file. Fails the build if not successful.
    /// </summary>
    /// <param name="this">The Cake context.</param>
    /// <param name="json">The JSON object to save.</param>
    /// <param name="path">The path of the file to save <paramref name="json"/> to.</param>
    public static void SaveJson(this ICakeContext @this, JsonNode json, FilePath path)
    {
        Guard.IsNotNull(json);
        Guard.IsNotNull(path);

        var fullPath = path.FullPath;
        try
        {
            using var stream = SysFile.OpenWrite(fullPath);
            var writerOptions = new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
            };

            using var writer = new Utf8JsonWriter(stream, writerOptions);
            json.WriteTo(writer);
            stream.SetLength(stream.Position);
        }
        catch (IOException e)
        {
            @this.Fail($"Could not write to {fullPath}: {e.Message}");
        }
    }

    /// <summary>
    /// Rewrites the value of one or more JSON string properties in a file in place, preserving every byte
    /// not covered by an actual replacement.
    /// </summary>
    /// <param name="this">The Cake context.</param>
    /// <param name="path">The path of the file to rewrite.</param>
    /// <param name="rewriter">A callback invoked once per string-valued property of an object reached during
    /// a depth-first walk of the document. Returning <see langword="null"/> (or the unchanged value) leaves
    /// the property alone; returning a different string queues a splice at that exact location.</param>
    /// <returns><see langword="true"/> if at least one property was actually changed and the file was rewritten;
    /// <see langword="false"/> if no callback returned a changed value (the file is left untouched on disk).</returns>
    /// <remarks>
    /// <para>Unlike a load-mutate-serialize cycle (e.g. <see cref="LoadJsonObject"/> + <see cref="SaveJson"/>),
    /// this method does not reformat the document: line endings, indentation, blank lines, comments, the
    /// trailing newline (if any) and a UTF-8 BOM (if any) are preserved exactly.</para>
    /// <para>Replacements are JSON-encoded with <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>,
    /// matching the policy used by <see cref="SaveJson"/>.</para>
    /// </remarks>
    public static bool RewriteJsonStringValues(this ICakeContext @this, FilePath path, JsonStringValueRewriter rewriter)
    {
        Guard.IsNotNull(path);
        Guard.IsNotNull(rewriter);

        var fullPath = path.FullPath;
        byte[] originalBytes;
        try
        {
            originalBytes = SysFile.ReadAllBytes(fullPath);
        }
        catch (IOException e)
        {
            return @this.Fail<bool>($"Could not read from {fullPath}: {e.Message}");
        }

        // Utf8JsonReader rejects a leading UTF-8 BOM; skip it for parsing but preserve it on rewrite.
        var bomLength = HasUtf8Bom(originalBytes) ? 3 : 0;

        List<JsonValueEdit> edits;
        try
        {
            edits = CollectJsonStringEdits(originalBytes.AsSpan(bomLength), bomLength, rewriter);
        }
        catch (JsonException)
        {
            return @this.Fail<bool>($"{fullPath} does not contain valid JSON.");
        }

        if (edits.Count == 0)
        {
            return false;
        }

        // Walker emits edits in source order, so a single forward pass over the original bytes suffices.
        using var output = new MemoryStream(originalBytes.Length + 64);
        var cursor = 0;
        foreach (var edit in edits)
        {
            if (edit.Start > cursor)
            {
                output.Write(originalBytes, cursor, edit.Start - cursor);
            }

            output.Write(edit.Replacement, 0, edit.Replacement.Length);
            cursor = edit.Start + edit.Length;
        }

        if (cursor < originalBytes.Length)
        {
            output.Write(originalBytes, cursor, originalBytes.Length - cursor);
        }

        try
        {
            SysFile.WriteAllBytes(fullPath, output.ToArray());
        }
        catch (IOException e)
        {
            @this.Fail($"Could not write to {fullPath}: {e.Message}");
        }

        return true;
    }

    /// <summary>
    /// Gets the value of a property from a JSON object. Fails the build if not successful.
    /// </summary>
    /// <typeparam name="T">The desired type of the property value.</typeparam>
    /// <param name="this">The Cake context.</param>
    /// <param name="json">The JSON object.</param>
    /// <param name="propertyName">The name of the property to get.</param>
    /// <param name="objectDescription">A description of the object for exception messages.</param>
    /// <returns>The value of the specified property.</returns>
    public static T GetJsonPropertyValue<T>(this ICakeContext @this, JsonObject json, string propertyName, string objectDescription = "JSON object")
    {
        Guard.IsNotNull(json);

        @this.Ensure(json.TryGetPropertyValue(propertyName, out var property), $"Json property {propertyName} not found in {objectDescription}.");
        switch (property)
        {
            case null:
                return @this.Fail<T>($"Json property {propertyName} in {objectDescription} is null.");
            case JsonValue value:
                @this.Ensure(value.TryGetValue<T>(out var result), $"Json property {propertyName} in {objectDescription} cannot be converted to a {typeof(T).Name}.");
                return result;
            default:
                return @this.Fail<T>($"Json property {propertyName} in {objectDescription} is a {property.GetType().Name}, not a {nameof(JsonValue)}.");
        }
    }

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
                        var currentValue = reader.GetString()!;
                        pathSegments.Add(pendingProperty);
                        var newValue = rewriter(pathSegments, currentValue);
                        pathSegments.RemoveAt(pathSegments.Count - 1);
                        pendingProperty = null;

                        if (newValue is not null && !string.Equals(newValue, currentValue, StringComparison.Ordinal))
                        {
                            // TokenStartIndex points to the opening quote; ValueSpan covers the bytes
                            // between the quotes (escape sequences included). The closing quote and any
                            // surrounding whitespace are deliberately untouched.
                            var innerStart = (int)reader.TokenStartIndex + 1 + offsetInFile;
                            var innerLength = reader.ValueSpan.Length;
                            var encoded = JsonEncodedText.Encode(newValue.AsSpan(), JavaScriptEncoder.UnsafeRelaxedJsonEscaping).EncodedUtf8Bytes.ToArray();
                            edits.Add(new JsonValueEdit(innerStart, innerLength, encoded));
                        }
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
}
