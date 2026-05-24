// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Provides the shared <see cref="JsonSerializerOptions"/> used to read and describe Buildvana configuration files.
/// </summary>
/// <remarks>
/// <para>The same options drive both deserialization and schema generation, so the committed schema always
/// reflects exactly what the loader accepts.</para>
/// </remarks>
public static class BuildvanaConfigSerialization
{
    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> used to deserialize and generate schemas for <see cref="BuildvanaConfig"/>.
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
        return options;
    }
}
