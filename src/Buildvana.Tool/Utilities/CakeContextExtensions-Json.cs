// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

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
}
