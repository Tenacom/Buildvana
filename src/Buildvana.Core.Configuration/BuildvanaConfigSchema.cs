// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.JsonSchema;
using Buildvana.Runtime;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Generates the JSON schema describing the Buildvana configuration file from the <see cref="BuildvanaConfig"/> model.
/// </summary>
public static class BuildvanaConfigSchema
{
    // The model, being packaged, cannot carry [JsonSchemaTitle] (see the comment on BuildvanaConfig),
    // so the title is supplied here.
    private const string Title = "Buildvana configuration";

    /// <summary>
    /// Generates the JSON schema for <see cref="BuildvanaConfig"/>.
    /// </summary>
    /// <returns>The schema as an indented JSON string, using LF line endings and a trailing newline.</returns>
    /// <remarks>
    /// <para>The schema is shaped from attributes on the model (<c>[Description]</c> and the
    /// <c>System.Text.Json</c> attributes) by <see cref="JsonSchemaGenerator"/>, driven by
    /// <see cref="BuildvanaConfigSerialization.Options"/>.</para>
    /// </remarks>
    public static string Generate()
    {
        var schema = JsonSchemaGenerator.Generate<BuildvanaConfig>(BuildvanaConfigSerialization.Options, Title);
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
