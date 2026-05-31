// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Generates a JSON Schema (draft 2020-12) document from a .NET type, shaping the output from attributes the
/// model carries: <see cref="DescriptionAttribute"/>, <see cref="JsonNullableAttribute"/>,
/// <see cref="JsonAllowedKeysAttribute"/>, and <see cref="JsonSchemaTitleAttribute"/>.
/// </summary>
/// <remarks>
/// <para>The same <see cref="JsonSerializerOptions"/> should drive both generation and deserialization, so the
/// schema always describes exactly what the deserializer accepts.</para>
/// <para><see cref="System.Text.Json"/> marks every reference-type dictionary value and collection element
/// nullable regardless of how the model annotates it, so the generator reconciles that against the declared
/// nullability read from the owning property or field via <see cref="NullabilityInfoContext"/>. This requires
/// a member to read the annotations from: when the type being described is <em>itself</em> a dictionary or
/// collection (so its values or elements have no owning member), their declared nullability cannot be
/// recovered and the nullability emitted by the exporter is kept as-is. Wrap such a type in a containing
/// object property to control the nullability of its values or elements.</para>
/// </remarks>
public static class JsonSchemaGenerator
{
    private const string Dialect = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>
    /// Generates the JSON schema describing <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to describe.</typeparam>
    /// <param name="options">The serializer options that govern property naming, enum formatting, and so on.</param>
    /// <returns>The schema as a <see cref="JsonNode"/>.</returns>
    public static JsonNode Generate<T>(JsonSerializerOptions options) => Generate(typeof(T), options);

    /// <summary>
    /// Generates the JSON schema describing <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The type to describe.</param>
    /// <param name="options">The serializer options that govern property naming, enum formatting, and so on.</param>
    /// <returns>The schema as a <see cref="JsonNode"/>.</returns>
    public static JsonNode Generate(Type type, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);

        var nullabilityContext = new NullabilityInfoContext();
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = (context, schema) => TransformSchemaNode(context, schema, nullabilityContext),
        };
        var schema = options.GetJsonSchemaAsNode(type, exporterOptions);

        // Declare the dialect and (optionally) a title so editors recognize and label the document.
        if (schema is JsonObject root)
        {
            root.Insert(0, "$schema", Dialect);
            if (type.GetCustomAttribute<JsonSchemaTitleAttribute>() is { Title: var title })
            {
                root.Insert(1, "title", title);
            }
        }

        return schema;
    }

    private static JsonNode TransformSchemaNode(
        JsonSchemaExporterContext context,
        JsonNode schema,
        NullabilityInfoContext nullabilityContext)
    {
        var attributeProvider = context.PropertyInfo is not null
            ? context.PropertyInfo.AttributeProvider
            : context.TypeInfo.Type;

        schema = ApplyDescription(attributeProvider, schema);

        // Strip the "null" the exporter adds to a property's own type: a nullable property means "optional"
        // (an absent key already expresses "unset"), so an explicit null is redundant unless the property
        // opts in with [JsonNullable]. Value and element nodes skip this — their nullability is reconciled
        // by the owning property below, because the exporter marks every reference-type value or element
        // nullable regardless of how the model actually declares it.
        var isValueOrElement = context.PropertyInfo is null && !context.Path.IsEmpty;
        var keepNull = attributeProvider?.IsDefined(typeof(JsonNullableAttribute), inherit: true) ?? false;
        if (!isValueOrElement && !keepNull && schema is JsonObject ownSchema)
        {
            RemoveNullFromType(ownSchema);
            RemoveNullFromEnum(ownSchema);
        }

        // Reconcile the nullability the exporter put on this property's values and elements with what the
        // model actually declares (string vs string?), recursing through nested generics. This runs before
        // ConstrainKeys so the keys it clones inherit the corrected value schema.
        if (context.PropertyInfo?.AttributeProvider is MemberInfo member && schema is JsonObject propertySchema)
        {
            var nullability = CreateNullabilityInfo(nullabilityContext, member);
            if (nullability is not null)
            {
                ReconcileValueNullability(propertySchema, nullability);
            }
        }

        // Close a dictionary to a fixed set of keys when the property carries [JsonAllowedKeys].
        if (TryGetAllowedKeys(attributeProvider, out var keys) && schema is JsonObject dictionarySchema)
        {
            ConstrainKeys(dictionarySchema, keys);
        }

        return schema;
    }

    private static NullabilityInfo? CreateNullabilityInfo(NullabilityInfoContext context, MemberInfo member)
        => member switch
        {
            PropertyInfo property => context.Create(property),
            FieldInfo field => context.Create(field),
            _ => null,
        };

    // Walks a property schema's value ("additionalProperties") and element ("items") subschemas alongside
    // the matching nullability metadata, keeping "null" only where the model declares the value or element
    // nullable. Recurses so nested generics (a dictionary of lists, say) are handled at every level.
    private static void ReconcileValueNullability(JsonObject schema, NullabilityInfo nullability)
    {
        if (schema["additionalProperties"] is JsonObject valueSchema)
        {
            ApplyDeclaredNullability(valueSchema, GetValueNullability(nullability));
        }

        if (schema["items"] is JsonObject itemSchema)
        {
            ApplyDeclaredNullability(itemSchema, GetElementNullability(nullability));
        }
    }

    private static void ApplyDeclaredNullability(JsonObject schema, NullabilityInfo? nullability)
    {
        if (nullability is null)
        {
            return;
        }

        if (nullability.ReadState != NullabilityState.Nullable)
        {
            RemoveNullFromType(schema);
            RemoveNullFromEnum(schema);
        }

        ReconcileValueNullability(schema, nullability);
    }

    // The value type of a dictionary is its last generic argument (IReadOnlyDictionary<TKey, TValue>).
    private static NullabilityInfo? GetValueNullability(NullabilityInfo nullability)
    {
        var args = nullability.GenericTypeArguments;
        return args.Length > 0 ? args[^1] : null;
    }

    // The element type is the array element, or the single generic argument of a collection.
    private static NullabilityInfo? GetElementNullability(NullabilityInfo nullability)
    {
        if (nullability.ElementType is { } elementType)
        {
            return elementType;
        }

        var args = nullability.GenericTypeArguments;
        return args.Length == 1 ? args[0] : null;
    }

    // Surfaces a [Description] (on the property, or on the type) as a schema "description" keyword.
    // Adapted from the System.Text.Json schema-exporter documentation sample.
    private static JsonNode ApplyDescription(ICustomAttributeProvider? attributeProvider, JsonNode schema)
    {
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

    private static bool TryGetAllowedKeys(ICustomAttributeProvider? attributeProvider, out IReadOnlyList<string> keys)
    {
        var attribute = attributeProvider?
            .GetCustomAttributes(inherit: true)
            .OfType<JsonAllowedKeysAttribute>()
            .FirstOrDefault();

        keys = attribute?.AllowedKeys ?? [];
        return attribute is not null;
    }

    // Replaces a dictionary's open-ended additionalProperties value schema with an explicit set of allowed keys,
    // each mapped to a clone of that value schema, plus additionalProperties: false.
    private static void ConstrainKeys(JsonObject schema, IReadOnlyList<string> keys)
    {
        if (schema["additionalProperties"] is not { } valueSchema)
        {
            return;
        }

        _ = schema.Remove("additionalProperties");

        var properties = new JsonObject();
        foreach (var key in keys)
        {
            properties.Add(key, valueSchema.DeepClone());
        }

        schema["properties"] = properties;
        schema["additionalProperties"] = false;
    }

    // Removes "null" from a schema's "type" keyword when it is expressed as an array, collapsing a single
    // remaining type to a scalar for cleaner output. No-op when "type" is absent or already a scalar.
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

    // Removes the JSON null member that the exporter appends to a nullable enum's "enum" list. No-op when the
    // schema has no "enum" keyword.
    private static void RemoveNullFromEnum(JsonObject schema)
    {
        if (schema["enum"] is not JsonArray enumArray)
        {
            return;
        }

        for (var i = enumArray.Count - 1; i >= 0; i--)
        {
            if (enumArray[i] is null)
            {
                enumArray.RemoveAt(i);
            }
        }
    }
}
