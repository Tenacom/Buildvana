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

            // The exporter leaves these dictionaries open-ended, but the loader only accepts a fixed set of keys;
            // pin the schema to the same keys so editors reject what the loader would reject.
            ConstrainKeys(root, "dotnet", "args", DotNetConfig.AllowedArgsKeys);
            ConstrainKeys(root, "nuget", "feeds", NuGetConfig.AllowedFeedKeys);

            // Collection element types in the model are non-nullable, but the exporter still emits nullable
            // item schemas; tighten them so the schema matches the model (and what the loader accepts).
            StripNullFromArrayItems(root);
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

    // Replaces the open-ended additionalProperties of a dictionary section (e.g. dotnet.args, nuget.feeds) with an
    // explicit set of allowed keys, each mapped to the original value schema, plus additionalProperties: false.
    private static void ConstrainKeys(JsonObject root, string topProperty, string dictProperty, string[] allowedKeys)
    {
        var section = (JsonObject)root["properties"]![topProperty]!["properties"]![dictProperty]!;
        var valueSchema = section["additionalProperties"]!;
        _ = section.Remove("additionalProperties");

        var properties = new JsonObject();
        foreach (var key in allowedKeys)
        {
            properties.Add(key, valueSchema.DeepClone());
        }

        section["properties"] = properties;
        section["additionalProperties"] = false;
    }

    // Walks the schema tree and removes "null" from the type of every array's item schema, since no collection
    // in the model accepts null elements.
    private static void StripNullFromArrayItems(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["items"] is JsonObject items)
                {
                    RemoveNullFromType(items);
                }

                foreach (var property in obj)
                {
                    StripNullFromArrayItems(property.Value);
                }

                break;
            case JsonArray array:
                foreach (var element in array)
                {
                    StripNullFromArrayItems(element);
                }

                break;
        }
    }

    // Removes "null" from a schema's "type" keyword when it is expressed as an array, collapsing a single
    // remaining type to a scalar for cleaner output. No-op when "type" is already a scalar.
    private static void RemoveNullFromType(JsonObject schema)
    {
        if (schema["type"] is not JsonArray typeArray)
        {
            return;
        }

        for (var i = typeArray.Count - 1; i >= 0; i--)
        {
            if (typeArray[i]?.GetValue<string>() == "null")
            {
                typeArray.RemoveAt(i);
            }
        }

        if (typeArray.Count == 1)
        {
            schema["type"] = typeArray[0]!.GetValue<string>();
        }
    }
}
