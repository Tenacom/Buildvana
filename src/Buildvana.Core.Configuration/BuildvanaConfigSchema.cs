// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

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
    /// <para><c>[Description]</c> attributes on the model are surfaced as <c>description</c> keywords, so editors
    /// can show them as hover documentation.</para>
    /// </remarks>
    public static string Generate()
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = TransformSchemaNode,
        };

        var schema = BuildvanaConfigSerialization.Options.GetJsonSchemaAsNode(typeof(BuildvanaConfig), exporterOptions);

        // Declare the JSON Schema dialect and a title so editors recognize and label the document.
        if (schema is JsonObject root)
        {
            root.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
            root.Insert(1, "title", "Buildvana configuration");

            // The exporter marks the root as nullable, but the loader rejects a JSON null document.
            root["type"] = "object";
        }

        var json = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
        });

        // Normalize to LF + a single trailing newline, independent of the host platform.
        return json.ReplaceLineEndings("\n") + "\n";
    }

    // Surfaces a [Description] (on the property, or on the type) as a schema "description" keyword.
    // Adapted from the System.Text.Json schema-exporter documentation sample.
    private static JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode schema)
    {
        var attributeProvider = context.PropertyInfo is not null
            ? context.PropertyInfo.AttributeProvider
            : context.TypeInfo.Type;

        var description = attributeProvider?
            .GetCustomAttributes(inherit: true)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?
            .Description;

        if (description is null)
        {
            return schema;
        }

        if (schema is not JsonObject schemaObject)
        {
            // A Boolean schema (true/false) cannot carry a description, so wrap it in an object first.
            var valueKind = schema.GetValueKind();
            schemaObject = new JsonObject();
            if (valueKind is JsonValueKind.False)
            {
                schemaObject.Add("not", true);
            }

            schema = schemaObject;
        }

        schemaObject.Insert(0, "description", description);
        return schema;
    }
}
