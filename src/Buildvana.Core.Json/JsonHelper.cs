// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Diagnostics;
using SysFile = System.IO.File;

namespace Buildvana.Core.Json;

/// <summary>
/// Default <see cref="IJsonHelper"/> implementation backed by <see cref="System.Text.Json"/> and the
/// host file system.
/// </summary>
public sealed partial class JsonHelper : IJsonHelper
{
    private readonly IBuildHost _host;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonHelper"/> class.
    /// </summary>
    /// <param name="host">The build host through which failures are reported.</param>
    public JsonHelper(IBuildHost host)
    {
        Guard.IsNotNull(host);
        _host = host;
    }

    /// <inheritdoc/>
    public JsonObject ParseObject(string str, string description = "The provided string")
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
            return _host.Fail<JsonObject>($"{description} is not valid JSON.");
        }

        return node switch {
            null => _host.Fail<JsonObject>($"{description} was parsed as JSON null."),
            JsonObject obj => obj,
            object other => _host.Fail<JsonObject>($"{description} was parsed as a {other.GetType().Name}, not a {nameof(JsonObject)}."),
        };
    }

    /// <inheritdoc/>
    public JsonObject LoadObject(string path)
    {
        Guard.IsNotNullOrEmpty(path);

        JsonNode? node;
        try
        {
            using var stream = SysFile.OpenRead(path);
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
            return _host.Fail<JsonObject>($"Could not read from {path}: {e.Message}");
        }
        catch (JsonException)
        {
            return _host.Fail<JsonObject>($"{path} does not contain valid JSON.");
        }

        return node switch {
            null => _host.Fail<JsonObject>($"{path} was parsed as JSON null."),
            JsonObject obj => obj,
            object other => _host.Fail<JsonObject>($"{path} was parsed as a {other.GetType().Name}, not a {nameof(JsonObject)}."),
        };
    }

    /// <inheritdoc/>
    public void SaveObject(JsonNode json, string path)
    {
        Guard.IsNotNull(json);
        Guard.IsNotNullOrEmpty(path);

        try
        {
            using var stream = SysFile.OpenWrite(path);
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
            _host.Fail($"Could not write to {path}: {e.Message}");
        }
    }

    /// <inheritdoc/>
    public bool RewriteStringValues(string path, JsonStringValueRewriter rewriter)
    {
        Guard.IsNotNullOrEmpty(path);
        Guard.IsNotNull(rewriter);

        byte[] originalBytes;
        try
        {
            originalBytes = SysFile.ReadAllBytes(path);
        }
        catch (IOException e)
        {
            return _host.Fail<bool>($"Could not read from {path}: {e.Message}");
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
            return _host.Fail<bool>($"{path} does not contain valid JSON.");
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
            SysFile.WriteAllBytes(path, output.ToArray());
        }
        catch (IOException e)
        {
            _host.Fail($"Could not write to {path}: {e.Message}");
        }

        return true;
    }

    /// <inheritdoc/>
    public T GetPropertyValue<T>(JsonObject json, string propertyName, string objectDescription = "JSON object")
    {
        Guard.IsNotNull(json);

        _host.Ensure(json.TryGetPropertyValue(propertyName, out var property), $"Json property {propertyName} not found in {objectDescription}.");
        switch (property)
        {
            case null:
                return _host.Fail<T>($"Json property {propertyName} in {objectDescription} is null.");
            case JsonValue value:
                _host.Ensure(value.TryGetValue<T>(out var result), $"Json property {propertyName} in {objectDescription} cannot be converted to a {typeof(T).Name}.");
                return result;
            default:
                return _host.Fail<T>($"Json property {propertyName} in {objectDescription} is a {property.GetType().Name}, not a {nameof(JsonValue)}.");
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
