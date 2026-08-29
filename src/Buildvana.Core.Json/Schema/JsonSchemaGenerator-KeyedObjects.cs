// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Buildvana.Core.Json.Schema;

partial class JsonSchemaGenerator
{
    // Renders IReadOnlyList<T>, where T carries [JsonKeyedObject], as the object shape
    // JsonKeyedObjectConverter reads and writes: one JSON property per element, with additionalProperties
    // describing the values. The exporter cannot see through the list's custom converter, so the whole node
    // is synthesized here from the element type's own schema.
    private static JsonObject CreateKeyedObjectSchema(
        Type elementType,
        JsonSerializerOptions options,
        NullabilityInfoContext nullabilityContext,
        HashSet<Type> keyedTypesInProgress)
    {
        // The exporter's own recursion handling ($ref) never sees a keyed list — the converter hides it — so
        // an element type that reaches a keyed list of itself would recurse through here forever.
        if (!keyedTypesInProgress.Add(elementType))
        {
            throw new InvalidOperationException(
                $"Keyed-object schema generation entered a cycle: element type '{elementType}' contains "
                + "a keyed-object list of its own type.");
        }

        try
        {
            var attribute = elementType.GetCustomAttribute<JsonKeyedObjectAttribute>(inherit: false)!;
            var (keyJsonName, valueJsonName) = JsonKeyedObjectConverter.ResolveKeyedNames(
                options.GetTypeInfo(elementType),
                attribute.KeyPropertyName,
                attribute.ValuePropertyName);
            var elementSchema = ExportSchema(elementType, options, nullabilityContext, keyedTypesInProgress) as JsonObject;
            if (elementSchema?["properties"] is not JsonObject elementProperties)
            {
                throw new InvalidOperationException(
                    $"The keyed-object element type '{elementType}' does not render as an object schema.");
            }

            // Read before the branch below: PruneKeyProperty strips the key from "required" on its way out.
            var keyIsRequired = IsRequired(elementSchema, keyJsonName);
            var valuesSchema = valueJsonName is not null
                ? LiftValueSchema(elementProperties, elementType, valueJsonName)
                : PruneKeyProperty(elementSchema, elementProperties, keyJsonName);
            ThrowIfContainsReference(elementType, valuesSchema);
            var keyedSchema = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = valuesSchema,
            };

            // The key travels as the member name, where a property's own constraints cannot reach it, so a
            // required key states through propertyNames what every other required string states about itself:
            // that a stated member carries an actual value. minLength catches the empty name, pattern the
            // all-whitespace one.
            if (keyIsRequired)
            {
                keyedSchema["propertyNames"] = new JsonObject
                {
                    ["minLength"] = 1,
                    ["pattern"] = @"\S",
                };
            }

            return keyedSchema;
        }
        finally
        {
            _ = keyedTypesInProgress.Remove(elementType);
        }
    }

    private static bool IsRequired(JsonObject elementSchema, string jsonName)
        => elementSchema["required"] is JsonArray required
            && required.Any(entry => entry?.GetValue<string>() == jsonName);

    // The value property's schema arrives with the property-level transforms (required-string constraints,
    // declared nullability, description) already applied by the element's own generation pass.
    private static JsonNode LiftValueSchema(JsonObject elementProperties, Type elementType, string valueJsonName)
    {
        var valueSchema = elementProperties[valueJsonName]
            ?? throw new InvalidOperationException(
                $"The schema of keyed-object element type '{elementType}' has no property '{valueJsonName}'.");

        // A node cannot be attached to the result while it still hangs off the discarded element schema.
        _ = elementProperties.Remove(valueJsonName);
        return valueSchema;
    }

    // The key travels as the JSON property name, so inside the value object it is pruned from "required" and
    // forbidden outright (a Boolean 'false' schema), because the converter refuses an element value that
    // restates it.
    private static JsonObject PruneKeyProperty(
        JsonObject elementSchema,
        JsonObject elementProperties,
        string keyJsonName)
    {
        elementProperties[keyJsonName] = false;
        if (elementSchema["required"] is JsonArray required)
        {
            for (var i = required.Count - 1; i >= 0; i--)
            {
                if (required[i]?.GetValue<string>() == keyJsonName)
                {
                    required.RemoveAt(i);
                }
            }

            if (required.Count == 0)
            {
                _ = elementSchema.Remove("required");
            }
        }

        return elementSchema;
    }

    // The exporter emits a root-relative "$ref" pointer for recursion, and also to deduplicate a repeated
    // (type, property) occurrence within one document. Embedded under additionalProperties, such a pointer
    // would resolve against the containing document instead of the element schema it was minted for, so the
    // schema is refused rather than emitted subtly wrong. The walk descends into the keywords this generator
    // emits subschemas under, plus "anyOf" — per the exporter's source, the only combinator it ever emits
    // (for polymorphic hierarchies).
    private static void ThrowIfContainsReference(Type elementType, JsonNode? schemaNode)
    {
        if (schemaNode is not JsonObject schema)
        {
            return;
        }

        if (schema.ContainsKey("$ref"))
        {
            throw new InvalidOperationException(
                $"The schema of keyed-object element type '{elementType}' contains a '$ref' pointer, "
                + "which cannot be embedded as a subschema.");
        }

        if (schema["properties"] is JsonObject properties)
        {
            foreach (var (_, propertySchema) in properties)
            {
                ThrowIfContainsReference(elementType, propertySchema);
            }
        }

        if (schema["anyOf"] is JsonArray branches)
        {
            foreach (var branch in branches)
            {
                ThrowIfContainsReference(elementType, branch);
            }
        }

        ThrowIfContainsReference(elementType, schema["additionalProperties"]);
        ThrowIfContainsReference(elementType, schema["items"]);
    }
}
