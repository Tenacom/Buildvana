// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Buildvana.Core.Json;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Provides the <see cref="JsonSerializerOptions"/> used to generate the schema describing Buildvana
/// configuration files.
/// </summary>
/// <remarks>
/// <para>Deserialization goes through the source-generated <see cref="BuildvanaJsonConfigContext"/> instead;
/// these reflection-based options exist because <see cref="System.Text.Json.Schema.JsonSchemaExporter"/> needs a
/// reflection-based resolver. They must mirror the context's options, so the committed schema always reflects
/// exactly what the deserializer accepts.</para>
/// </remarks>
public static class BuildvanaJsonConfigSerialization
{
    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used to generate schemas for
    /// <see cref="BuildvanaJsonConfig"/>.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            // Required by JsonSchemaExporter, which validates the options before generating a schema.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,

            // Disallow rejects unknown object members; dictionary keys are validated separately by the loader.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        // Reads the object-shaped ordered lists of the dependencies section. Registered here as well as on
        // the context, so the schema describes the object shape the deserializer actually reads.
        options.Converters.Add(new JsonKeyedObjectConverter());
        return options;
    }
}
