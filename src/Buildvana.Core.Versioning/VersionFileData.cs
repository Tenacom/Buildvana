// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Represents the immutable data parsed from a <c>current-version.json</c> file
/// (<c>{ "major": &lt;int&gt;, "minor": &lt;int&gt;, "prerelease": &lt;bool&gt; }</c>).
/// </summary>
/// <param name="Major">The major version component.</param>
/// <param name="Minor">The minor version component.</param>
/// <param name="Prerelease">A value indicating whether the version is a prerelease.</param>
/// <remarks>
/// <para>The <c>current-version.json</c> file holds the version <em>value</em>; release <em>policy</em>
/// (public-release branches, prerelease tag, assembly-version precision) lives in <c>buildvana.json</c>.</para>
/// <para>This type does not read the file from disk. Callers are responsible for reading the file content and
/// for <strong>failing loudly when the file is absent</strong>: a missing <c>current-version.json</c> must be
/// treated as an error, never silently defaulted.</para>
/// </remarks>
public sealed record VersionFileData(int Major, int Minor, bool Prerelease)
{
    /// <summary>
    /// Parses a <see cref="VersionFileData"/> from the textual content of a <c>current-version.json</c> file.
    /// </summary>
    /// <param name="json">The JSON content to parse.</param>
    /// <returns>The parsed <see cref="VersionFileData"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="json"/> is not a JSON object, is missing a required property, or has a property of the wrong
    /// type or value (<c>major</c> and <c>minor</c> must be non-negative integers, <c>prerelease</c> a boolean).
    /// </exception>
    public static VersionFileData Parse(string json)
    {
        Guard.IsNotNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new FormatException("current-version.json is not valid JSON.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("current-version.json must contain a JSON object.");
            }

            var major = GetNonNegativeInt32(root, "major");
            var minor = GetNonNegativeInt32(root, "minor");
            var prerelease = GetBoolean(root, "prerelease");
            return new VersionFileData(major, minor, prerelease);
        }
    }

    private static int GetNonNegativeInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new FormatException($"current-version.json is missing the required '{propertyName}' property.");
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) && value >= 0)
        {
            return value;
        }

        throw new FormatException($"current-version.json property '{propertyName}' must be a non-negative integer.");
    }

    private static bool GetBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new FormatException($"current-version.json is missing the required '{propertyName}' property.");
        }

        return element.ValueKind switch {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new FormatException($"current-version.json property '{propertyName}' must be a boolean."),
        };
    }
}
