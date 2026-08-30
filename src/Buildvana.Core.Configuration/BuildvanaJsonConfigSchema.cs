// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Buildvana.Core.Json.Schema;
using Buildvana.Runtime;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Generates the JSON schema describing the Buildvana configuration file from the
/// <see cref="BuildvanaJsonConfig"/> wire model.
/// </summary>
public static class BuildvanaJsonConfigSchema
{
    /// <summary>
    /// Generates the JSON schema for <see cref="BuildvanaJsonConfig"/>.
    /// </summary>
    /// <returns>The schema as an indented JSON string, using LF line endings and a trailing newline.</returns>
    /// <remarks>
    /// <para>The schema is shaped from attributes on the wire model (<c>[Description]</c>,
    /// <c>[JsonSchemaTitle]</c>, and the <c>System.Text.Json</c> attributes) by
    /// <see cref="JsonSchemaGenerator"/>, driven by <see cref="BuildvanaJsonConfigSerialization.Options"/>.</para>
    /// <para>Default annotations come from a fresh <see cref="BuildvanaConfig"/>: every built-in default lives
    /// on the domain model as a property initializer, so the schema documents exactly what an omitted setting
    /// resolves to.</para>
    /// </remarks>
    public static string Generate()
    {
        var json = GenerateNode().ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
        });

        // Normalize to LF + a single trailing newline, independent of the host platform.
        return json.ReplaceLineEndings("\n") + "\n";
    }

    // The same schema before it is serialized, for the caller that walks it rather than writing it out.
    // BuildvanaJsonConfigExample is the one such caller: it reads descriptions, defaults, examples, and
    // propertyNames straight off the nodes, so nothing in this repository parses the text back.
    internal static JsonNode GenerateNode()
        => JsonSchemaGenerator.Generate<BuildvanaJsonConfig>(
            BuildvanaJsonConfigSerialization.Options,
            defaults: new BuildvanaConfig());
}
