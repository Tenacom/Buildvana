// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Buildvana.Core.Json.Schema;

/// <summary>
/// Generates a JSON Schema (draft 2020-12) document from a .NET type, shaping the output from attributes the
/// model carries: <see cref="DescriptionAttribute"/>, <see cref="JsonNullableAttribute"/>,
/// <see cref="JsonAllowedKeysAttribute"/>, <see cref="JsonAllowedValuesAttribute"/>,
/// <see cref="JsonKeyedObjectAttribute"/>, <see cref="JsonSchemaNoDefaultAttribute"/>, and
/// <see cref="JsonSchemaTitleAttribute"/>. C# <c>required</c> members surface as the schema's
/// <c>required</c> keyword, courtesy of the underlying exporter; required string members additionally gain
/// <c>minLength</c> and <c>pattern</c> constraints demanding a non-blank value, unless
/// <see cref="JsonAllowedValuesAttribute"/> has already enumerated what the member may hold.
/// </summary>
/// <remarks>
/// <para>The same <see cref="JsonSerializerOptions"/> should drive both generation and deserialization, so the
/// schema always describes exactly what the deserializer accepts.</para>
/// <para>When <see cref="JsonKeyedObjectConverter"/> is registered in the options, an
/// <see cref="IReadOnlyList{T}"/> whose element type carries <see cref="JsonKeyedObjectAttribute"/> renders
/// as <c>type: object</c>, with <c>additionalProperties</c> describing the values: the value property's
/// schema when the attribute names one, the object schema of the remaining members otherwise. Without the
/// converter the list deserializes as a plain JSON array, which the schema then describes. In the
/// remaining-members shape the key property is forbidden inside the value object, because the converter
/// refuses an element value that restates it. Such a list is dictionary-valued in JSON, so
/// <see cref="JsonAllowedKeysAttribute"/> closes its key set the same way it closes a dictionary's. A
/// keyed-object list is still a collection: it states no <c>default</c>. An element type whose schema
/// carries a <c>$ref</c> pointer is not supported — recursion, or the exporter's deduplication of a member
/// type that occurs twice.</para>
/// <para>When a defaults instance is supplied, leaf properties (strings, numbers, booleans, enums) gain a
/// <c>default</c> keyword holding the matching property value from that instance, serialized with the same
/// options as the schema. Matching is by resolved JSON name, not by type: the instance may well be of a
/// different model than the one described — a resolved domain model carrying the defaults of a wire model,
/// say. Object-valued properties recurse; collections and null values state no default; and a property
/// carrying <see cref="JsonSchemaNoDefaultAttribute"/> is skipped — for defaults too dynamic to state
/// statically, or wire-only members with no domain counterpart. A schema property the defaults instance has
/// no matching property for is an error, never a silent skip. The <c>default</c> keyword is an annotation
/// for editors and documentation; validation never fills it in.</para>
/// <para><see cref="System.Text.Json"/> marks every reference-type dictionary value and collection element
/// nullable regardless of how the model annotates it, so the generator reconciles that against the declared
/// nullability read from the owning property or field via <see cref="NullabilityInfoContext"/>. This requires
/// a member to read the annotations from: when the type being described is <em>itself</em> a dictionary or
/// collection (so its values or elements have no owning member), their declared nullability cannot be
/// recovered and the nullability emitted by the exporter is kept as-is. Wrap such a type in a containing
/// object property to control the nullability of its values or elements.</para>
/// </remarks>
public static partial class JsonSchemaGenerator
{
    private const string Dialect = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>
    /// Generates the JSON schema describing <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to describe.</typeparam>
    /// <param name="options">The serializer options that govern property naming, enum formatting, and so on.</param>
    /// <param name="title">The schema title; when omitted, the type's <see cref="JsonSchemaTitleAttribute"/>
    /// (if any) supplies it. Useful when the type cannot carry the attribute.</param>
    /// <param name="defaults">An instance whose property values become <c>default</c> annotations in the
    /// schema, or <see langword="null"/> to emit none. Matched to schema properties by JSON name, so it does
    /// not have to be of the described type; see the remarks.</param>
    /// <returns>The schema as a <see cref="JsonNode"/>.</returns>
    public static JsonNode Generate<T>(JsonSerializerOptions options, string? title = null, object? defaults = null)
        => Generate(typeof(T), options, title, defaults);

    /// <summary>
    /// Generates the JSON schema describing <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The type to describe.</param>
    /// <param name="options">The serializer options that govern property naming, enum formatting, and so on.</param>
    /// <param name="title">The schema title; when omitted, the type's <see cref="JsonSchemaTitleAttribute"/>
    /// (if any) supplies it. Useful when the type cannot carry the attribute.</param>
    /// <param name="defaults">An instance whose property values become <c>default</c> annotations in the
    /// schema, or <see langword="null"/> to emit none. Matched to schema properties by JSON name, so it does
    /// not have to be of the described type; see the remarks.</param>
    /// <returns>The schema as a <see cref="JsonNode"/>.</returns>
    public static JsonNode Generate(
        Type type,
        JsonSerializerOptions options,
        string? title = null,
        object? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);

        var schema = ExportSchema(type, options, new NullabilityInfoContext(), []);
        if (schema is not JsonObject root)
        {
            return schema;
        }

        // Declare the dialect and (optionally) a title so editors recognize and label the document.
        root.Insert(0, "$schema", Dialect);
        title ??= type.GetCustomAttribute<JsonSchemaTitleAttribute>()?.Title;
        if (title is not null)
        {
            root.Insert(1, "title", title);
        }

        if (defaults is not null)
        {
            ApplyDefaults(root, type, defaults, options);
        }

        return root;
    }

    // One exporter invocation with this generator's transform attached. Also called per keyed-object element
    // type, so nullability metadata and the in-progress cycle guard flow through every nesting level.
    private static JsonNode ExportSchema(
        Type type,
        JsonSerializerOptions options,
        NullabilityInfoContext nullabilityContext,
        HashSet<Type> keyedTypesInProgress)
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = (context, schema)
                => TransformSchemaNode(context, schema, nullabilityContext, keyedTypesInProgress),
        };
        return options.GetJsonSchemaAsNode(type, exporterOptions);
    }

    private static JsonNode TransformSchemaNode(
        JsonSchemaExporterContext context,
        JsonNode schema,
        NullabilityInfoContext nullabilityContext,
        HashSet<Type> keyedTypesInProgress)
    {
        var attributeProvider = context.PropertyInfo is not null
            ? context.PropertyInfo.AttributeProvider
            : context.TypeInfo.Type;
        var keepNull = attributeProvider?.IsDefined(typeof(JsonNullableAttribute), inherit: true) ?? false;

        // A keyed-object list renders as an object, not an array. The exporter cannot see through the list's
        // custom converter and emits an unconstrained node, so the node is replaced wholesale with a schema
        // synthesized from the element type; the steps below shape what the exporter emitted and do not apply.
        // The kind gate checks that a custom converter actually serves the type here (JsonTypeInfoKind.None):
        // without JsonKeyedObjectConverter in the options, the list deserializes as a plain JSON array, and
        // the exporter's own array schema stands.
        var keyedElementType = context.TypeInfo.Kind is JsonTypeInfoKind.None
            ? JsonKeyedObjectConverter.TryGetKeyedElementType(context.TypeInfo.Type)
            : null;
        if (keyedElementType is not null)
        {
            var keyedSchema = CreateKeyedObjectSchema(
                keyedElementType,
                context.TypeInfo.Options,
                nullabilityContext,
                keyedTypesInProgress);
            if (keepNull)
            {
                keyedSchema["type"] = new JsonArray("object", "null");
            }

            // A keyed-object list is dictionary-valued in JSON, so [JsonAllowedKeys] closes its key set the
            // same way it closes a dictionary's.
            if (TryGetAllowedKeys(attributeProvider, out var allowedKeys))
            {
                ConstrainKeys(keyedSchema, allowedKeys);
            }

            return ApplyDescription(attributeProvider, keyedSchema);
        }

        schema = ApplyDescription(attributeProvider, schema);

        // Strip the "null" the exporter adds to a property's own type: a nullable property means "optional"
        // (an absent key already expresses "unset"), so an explicit null is redundant unless the property
        // opts in with [JsonNullable]. Value and element nodes skip this — their nullability is reconciled
        // by the owning property below, because the exporter marks every reference-type value or element
        // nullable regardless of how the model actually declares it.
        var isValueOrElement = context.PropertyInfo is null && !context.Path.IsEmpty;
        if (!isValueOrElement && !keepNull && schema is JsonObject ownSchema)
        {
            RemoveNullFromType(ownSchema);
            RemoveNullFromEnum(ownSchema);
        }

        // Replace a string member's open value space with the set the model enumerates. This runs before the
        // required-string constraints below, which an explicit set makes redundant.
        if (TryGetAllowedValues(attributeProvider, out var allowedValues) && schema is JsonObject valuesSchema)
        {
            ConstrainValues(valuesSchema, allowedValues);
        }

        // A required string member must state an actual value: an empty or all-whitespace string satisfies
        // the `required` keyword (which only checks presence) while carrying no value, so required strings
        // are additionally constrained to at least one non-whitespace character.
        if (context.PropertyInfo is { IsRequired: true } && schema is JsonObject requiredSchema)
        {
            RequireNonBlankString(requiredSchema);
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

    private static bool TryGetAllowedValues(ICustomAttributeProvider? attributeProvider, out IReadOnlyList<string> values)
    {
        var attribute = attributeProvider?
            .GetCustomAttributes(inherit: true)
            .OfType<JsonAllowedValuesAttribute>()
            .FirstOrDefault();

        values = attribute?.AllowedValues ?? [];
        return attribute is not null;
    }

    // Replaces a string schema's open value space with an explicit set of allowed values. Schemas that do not
    // describe a string are left untouched, as JSON Schema's own string keywords are.
    private static void ConstrainValues(JsonObject schema, IReadOnlyList<string> values)
    {
        if (!SchemaTypeIncludesString(schema))
        {
            return;
        }

        // JsonValue.Create, not the generic JsonArray.Add<T>: the latter serializes its argument through the
        // ambient serializer options, which resolve no metadata at all in a file-based app — where the schema
        // generator runs, reflection-based serialization being disabled there.
        var allowed = new JsonArray();
        foreach (var value in values)
        {
            allowed.Add(JsonValue.Create(value));
        }

        // An enumerated set is the whole value space, so a member whose type still admits null — [JsonNullable]
        // is what leaves it there — keeps null in the set too. Without this the schema would reject the very
        // value its own type advertises. The exporter does the same for a nullable enum property.
        if (SchemaTypeIncludesNull(schema))
        {
            allowed.Add(null);
        }

        schema["enum"] = allowed;
    }

    // minLength catches the empty string with a clear message; pattern catches all-whitespace values, which
    // minLength alone would accept. Schemas that do not describe a string are left untouched, and so are
    // those whose values are already enumerated: neither a blank nor any other unwanted value is in the set.
    private static void RequireNonBlankString(JsonObject schema)
    {
        if (!SchemaTypeIncludesString(schema) || schema.ContainsKey("enum"))
        {
            return;
        }

        schema["minLength"] = 1;
        schema["pattern"] = @"\S";
    }

    private static bool SchemaTypeIncludesString(JsonObject schema) => SchemaTypeIncludes(schema, "string");

    private static bool SchemaTypeIncludesNull(JsonObject schema) => SchemaTypeIncludes(schema, "null");

    private static bool SchemaTypeIncludes(JsonObject schema, string typeName)
        => schema["type"] switch
        {
            JsonValue value => value.GetValue<string>() == typeName,
            JsonArray array => array.Any(t => t?.GetValue<string>() == typeName),
            _ => false,
        };

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

    // Annotates the schema's property subschemas with "default" keywords read from a defaults instance,
    // matching members by resolved JSON name on both sides. A [JsonAllowedKeys] dictionary also carries
    // "properties", but its keys name no member of the schema type, so its entries fall through harmlessly.
    // A schema member the defaults instance cannot answer for, though, is an error, not a skip: matching is
    // by name alone, so silently dropping the miss is how a rename on either side of a model pair would cost
    // a whole section its defaults without any signal.
    private static void ApplyDefaults(
        JsonObject schema,
        Type schemaType,
        object defaults,
        JsonSerializerOptions options)
    {
        if (schema["properties"] is not JsonObject properties)
        {
            return;
        }

        foreach (var (jsonName, propertyNode) in properties)
        {
            if (propertyNode is not JsonObject propertySchema)
            {
                continue;
            }

            var schemaProperty = FindPropertyByJsonName(schemaType, jsonName, options);
            var noDefault = schemaProperty?.IsDefined(typeof(JsonSchemaNoDefaultAttribute), inherit: true) ?? false;
            if (schemaProperty is null || noDefault)
            {
                continue;
            }

            var defaultsProperty = FindPropertyByJsonName(defaults.GetType(), jsonName, options)
                ?? throw new InvalidOperationException(
                    $"The defaults type '{defaults.GetType()}' has no property whose JSON name is '{jsonName}'. "
                    + "Annotate the schema property with [JsonSchemaNoDefault] if it deliberately has no default.");
            var value = defaultsProperty.GetValue(defaults);
            if (value is null)
            {
                continue;
            }

            if (IsLeafValue(value))
            {
                propertySchema["default"] = JsonSerializer.SerializeToNode(value, value.GetType(), options);
            }
            else if (value is not IEnumerable)
            {
                ApplyDefaults(propertySchema, schemaProperty.PropertyType, value, options);
            }
        }
    }

    private static PropertyInfo? FindPropertyByJsonName(Type type, string jsonName, JsonSerializerOptions options)
        => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(property => GetJsonName(property, options) == jsonName);

    private static string GetJsonName(PropertyInfo property, JsonSerializerOptions options)
        => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? options.PropertyNamingPolicy?.ConvertName(property.Name)
            ?? property.Name;

    // A leaf renders as a single JSON value: the "default" keyword states it outright. Everything else is
    // either an object (recursed into) or a collection (no default).
    private static bool IsLeafValue(object value)
    {
        var type = value.GetType();
        return type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal);
    }
}
