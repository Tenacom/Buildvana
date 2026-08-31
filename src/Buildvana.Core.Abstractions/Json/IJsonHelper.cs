// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Buildvana.Core.Json;

/// <summary>
/// <para>Provides JSON loading, parsing, saving, and in-place rewriting helpers.
/// On parse, I/O, or shape errors, implementations throw a <see cref="BuildFailedException"/>.</para>
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
    // ReSharper disable once UnusedMemberInSuper.Global // SaveObject is half of the load/save contract, so it stays, even if it has no current usage.
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
    /// Rewrites the value of one or more JSON boolean properties in a file in place, preserving every byte
    /// not covered by an actual replacement.
    /// </summary>
    /// <param name="path">The path of the file to rewrite.</param>
    /// <param name="rewriter">A callback invoked once per boolean-valued property of an object reached during
    /// a depth-first walk of the document. Returning <see langword="null"/> (or the unchanged value) leaves
    /// the property alone; returning the other value queues a splice at that exact location.</param>
    /// <returns><see langword="true"/> if at least one property was actually changed and the file was rewritten;
    /// <see langword="false"/> if no callback returned a changed value (the file is left untouched on disk).</returns>
    /// <remarks>
    /// <para>What <see cref="RewriteStringValues"/> does for a string value, this does for a boolean one, and
    /// with the same promise: the document is not reformatted, so line endings, indentation, blank lines,
    /// comments, the trailing newline (if any) and a UTF-8 BOM (if any) are preserved exactly.</para>
    /// </remarks>
    bool RewriteBooleanValues(string path, JsonBooleanValueRewriter rewriter);

    /// <summary>
    /// Inserts a property into an object of a JSON file in place, preserving every byte outside the
    /// insertion point.
    /// </summary>
    /// <param name="path">The path of the file to rewrite.</param>
    /// <param name="parentPath">The property path of the object to insert into: property names from the
    /// root object down, an empty list denoting the root object itself. Fails the build if the path does
    /// not lead to an object. Array elements contribute no path segment, so a path can also lead into an
    /// object nested inside an array element (e.g. <c>["a", "b"]</c> matches the <c>b</c> property of an
    /// object element of the <c>a</c> array); callers are expected to pass paths that do not traverse
    /// arrays.</param>
    /// <param name="propertyName">The name of the property to insert.</param>
    /// <param name="value">The value of the property to insert.</param>
    /// <returns><see langword="true"/> if the property was inserted; <see langword="false"/> if the object
    /// already has a property with the given name (the file is left untouched on disk).</returns>
    /// <remarks>
    /// <para>The property is inserted as the first property of the object, mimicking the surrounding
    /// formatting: line endings, indentation, comments, the trailing newline (if any) and a UTF-8 BOM
    /// (if any) are preserved. Multi-line values are indented to match the insertion point. When the target
    /// object is empty there is no sibling property to mimic: the object's body, and the nested lines of a
    /// multi-line value, are then indented by two spaces per level regardless of the file's own indentation
    /// unit.</para>
    /// </remarks>
    bool InsertProperty(string path, IReadOnlyList<string> parentPath, string propertyName, JsonNode value);

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
