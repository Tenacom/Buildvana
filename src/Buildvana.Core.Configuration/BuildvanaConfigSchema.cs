// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.JsonSchema;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Generates the JSON schema describing the Buildvana configuration file from the <see cref="BuildvanaConfig"/> model.
/// </summary>
public static class BuildvanaConfigSchema
{
    /// <summary>
    /// Generates the JSON schema for <see cref="BuildvanaConfig"/>.
    /// </summary>
    /// <returns>The schema as an indented JSON string, using LF line endings and a trailing newline.</returns>
    /// <remarks>
    /// <para>The schema is shaped entirely from attributes on the model — <c>[Description]</c>,
    /// <c>[JsonNullable]</c>, <c>[JsonAllowedKeys]</c>, and <c>[JsonSchemaTitle]</c> — by
    /// <see cref="JsonSchemaGenerator"/>. The same <see cref="BuildvanaConfigSerialization.Options"/> drive both
    /// generation and deserialization, so the schema always describes what the loader accepts.</para>
    /// </remarks>
    public static string Generate()
    {
        var schema = JsonSchemaGenerator.Generate<BuildvanaConfig>(BuildvanaConfigSerialization.Options);
        var json = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
        });

        // Normalize to LF + a single trailing newline, independent of the host platform.
        return json.ReplaceLineEndings("\n") + "\n";
    }
}
