// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

namespace Buildvana.Core.Json;

/// <summary>
/// <para>Provides JSON loading, parsing, saving, and in-place rewriting helpers.
/// On parse, I/O, or shape errors, implementations report the failure through their associated
/// <see cref="IBuildHost"/> (typically via <see cref="IBuildHost.Fail(string)"/>) and do not return.</para>
/// </summary>
public interface IJsonHelper
{
    /// <summary>
    /// Parses a JSON object from a string. Fails the build if not successful.
    /// </summary>
    /// <param name="str">The string to parse.</param>
    /// <param name="description">A description of the string for failure messages.</param>
    /// <returns>The parsed object.</returns>
    JsonObject ParseObject(string str, string description = "The provided string");

    /// <summary>
    /// Loads a JSON object from a file. Fails the build if not successful.
    /// </summary>
    /// <param name="path">The path of the file to parse.</param>
    /// <returns>The parsed object.</returns>
    JsonObject LoadObject(string path);

    /// <summary>
    /// Saves a JSON object to a file. Fails the build if not successful.
    /// </summary>
    /// <param name="json">The JSON object to save.</param>
    /// <param name="path">The path of the file to save <paramref name="json"/> to.</param>
    void SaveObject(JsonNode json, string path);

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
    /// <para>Unlike a load-mutate-serialize cycle (e.g. <see cref="LoadObject"/> + <see cref="SaveObject"/>),
    /// this method does not reformat the document: line endings, indentation, blank lines, comments, the
    /// trailing newline (if any) and a UTF-8 BOM (if any) are preserved exactly.</para>
    /// </remarks>
    bool RewriteStringValues(string path, JsonStringValueRewriter rewriter);

    /// <summary>
    /// Gets the value of a property from a JSON object. Fails the build if not successful.
    /// </summary>
    /// <typeparam name="T">The desired type of the property value.</typeparam>
    /// <param name="json">The JSON object.</param>
    /// <param name="propertyName">The name of the property to get.</param>
    /// <param name="objectDescription">A description of the object for failure messages.</param>
    /// <returns>The value of the specified property.</returns>
    T GetPropertyValue<T>(JsonObject json, string propertyName, string objectDescription = "JSON object");
}
