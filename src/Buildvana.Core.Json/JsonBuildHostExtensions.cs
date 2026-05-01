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
/// Provides JSON-related helpers that fail the build through an <see cref="IBuildHost"/> on parse or I/O errors.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static partial class JsonBuildHostExtensions
{
    extension(IBuildHost @this)
    {
        /// <summary>
        /// Parses a JSON object from a string. Fails the build if not successful.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <param name="description">A description of the string for exception messages.</param>
        /// <returns>The parsed object.</returns>
        public JsonObject ParseJsonObject(string str, string description = "The provided string")
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
        /// <param name="path">The path of the file to parse.</param>
        /// <returns>The parsed object.</returns>
        public JsonObject LoadJsonObject(string path)
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
                return @this.Fail<JsonObject>($"Could not read from {path}: {e.Message}");
            }
            catch (JsonException)
            {
                return @this.Fail<JsonObject>($"{path} does not contain valid JSON.");
            }

            return node switch {
                null => @this.Fail<JsonObject>($"{path} was parsed as JSON null."),
                JsonObject obj => obj,
                object other => @this.Fail<JsonObject>($"{path} was parsed as a {other.GetType().Name}, not a {nameof(JsonObject)}."),
            };
        }

        /// <summary>
        /// Saves a JSON object to a file. Fails the build if not successful.
        /// </summary>
        /// <param name="json">The JSON object to save.</param>
        /// <param name="path">The path of the file to save <paramref name="json"/> to.</param>
        public void SaveJson(JsonNode json, string path)
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
                @this.Fail($"Could not write to {path}: {e.Message}");
            }
        }

        /// <summary>
        /// Rewrites the value of one or more JSON string properties in a file in place, preserving every byte
        /// not covered by an actual replacement.
        /// </summary>
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
        public bool RewriteJsonStringValues(string path, JsonStringValueRewriter rewriter)
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
                return @this.Fail<bool>($"Could not read from {path}: {e.Message}");
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
                return @this.Fail<bool>($"{path} does not contain valid JSON.");
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
                @this.Fail($"Could not write to {path}: {e.Message}");
            }

            return true;
        }

        /// <summary>
        /// Gets the value of a property from a JSON object. Fails the build if not successful.
        /// </summary>
        /// <typeparam name="T">The desired type of the property value.</typeparam>
        /// <param name="json">The JSON object.</param>
        /// <param name="propertyName">The name of the property to get.</param>
        /// <param name="objectDescription">A description of the object for exception messages.</param>
        /// <returns>The value of the specified property.</returns>
        public T GetJsonPropertyValue<T>(JsonObject json, string propertyName, string objectDescription = "JSON object")
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
    }
}
