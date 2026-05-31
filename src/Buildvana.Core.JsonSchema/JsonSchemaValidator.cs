// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Validates a <see cref="JsonNode"/> against the subset of JSON Schema (draft 2020-12) keywords that
/// <c>JsonSchemaGenerator</c> emits: <c>type</c>, <c>enum</c>, <c>properties</c>, <c>required</c>,
/// <c>additionalProperties</c>, and <c>items</c>. Meta keywords such as <c>$schema</c>, <c>title</c>, and
/// <c>description</c> are ignored.
/// </summary>
public static class JsonSchemaValidator
{
    /// <summary>
    /// Generates the schema for <typeparamref name="T"/>, validates <paramref name="instance"/> against it, and
    /// resolves each error's source position from <paramref name="utf8Json"/> — so a caller supplies only the
    /// model type, not a schema or a source map.
    /// </summary>
    /// <typeparam name="T">The type whose schema <paramref name="instance"/> must conform to.</typeparam>
    /// <param name="instance">The JSON value to validate. <see langword="null"/> represents a JSON null.</param>
    /// <param name="utf8Json">The UTF-8 bytes that <paramref name="instance"/> was parsed from.</param>
    /// <param name="options">The serializer options that drive schema generation.</param>
    /// <returns>
    /// The validation errors found, each carrying its 1-based line and column; an empty list when valid.
    /// </returns>
    /// <remarks>
    /// <para>The schema is generated on every call. To validate many documents against one schema, call
    /// <see cref="JsonSchemaGenerator.Generate{T}(JsonSerializerOptions)"/> once and pass the result to
    /// <see cref="Validate(JsonNode?, JsonNode, ReadOnlySpan{byte})"/>.</para>
    /// </remarks>
    public static IReadOnlyList<JsonSchemaValidationError> Validate<T>(
        JsonNode? instance,
        ReadOnlySpan<byte> utf8Json,
        JsonSerializerOptions options)
        => Validate(instance, JsonSchemaGenerator.Generate<T>(options), utf8Json);

    /// <summary>
    /// Validates <paramref name="instance"/> against <paramref name="schema"/> and returns any failures.
    /// </summary>
    /// <param name="instance">The JSON value to validate. <see langword="null"/> represents a JSON null.</param>
    /// <param name="schema">The schema to validate against.</param>
    /// <returns>The validation errors found, or an empty list when <paramref name="instance"/> is valid.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="schema"/> is malformed: it contains an unresolvable or circular <c>$ref</c>.
    /// </exception>
    public static IReadOnlyList<JsonSchemaValidationError> Validate(JsonNode? instance, JsonNode schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var errors = new List<JsonSchemaValidationError>();
        ValidateNode(instance, schema, string.Empty, string.Empty, schema, [], errors);
        return errors;
    }

    /// <summary>
    /// Validates <paramref name="instance"/> against <paramref name="schema"/> and resolves each error's source
    /// position from <paramref name="utf8Json"/>, so callers do not each have to build a source map and translate
    /// pointers to positions.
    /// </summary>
    /// <param name="instance">The JSON value to validate. <see langword="null"/> represents a JSON null.</param>
    /// <param name="schema">The schema to validate against.</param>
    /// <param name="utf8Json">The UTF-8 bytes that <paramref name="instance"/> was parsed from.</param>
    /// <returns>
    /// The validation errors found, each carrying its 1-based <see cref="JsonSchemaValidationError.Line"/> and
    /// <see cref="JsonSchemaValidationError.Column"/>; an empty list when <paramref name="instance"/> is valid.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="schema"/> is malformed: it contains an unresolvable or circular <c>$ref</c>.
    /// </exception>
    public static IReadOnlyList<JsonSchemaValidationError> Validate(
        JsonNode? instance,
        JsonNode schema,
        ReadOnlySpan<byte> utf8Json)
    {
        var errors = Validate(instance, schema);
        if (errors.Count == 0)
        {
            return errors;
        }

        var sourceMap = JsonSourceMap.Build(utf8Json);
        var located = new List<JsonSchemaValidationError>(errors.Count);
        foreach (var error in errors)
        {
            _ = sourceMap.TryGetPosition(error.JsonPointer, out var line, out var column);
            located.Add(error with { Line = line, Column = column });
        }

        return located;
    }

    private static void ValidateNode(
        JsonNode? instance,
        JsonNode schemaNode,
        string pointer,
        string displayPath,
        JsonNode root,
        HashSet<string> visitedRefs,
        List<JsonSchemaValidationError> errors)
    {
        // A Boolean schema accepts (true) or rejects (false) every instance.
        if (schemaNode is JsonValue)
        {
            if (schemaNode.GetValueKind() is JsonValueKind.False)
            {
                errors.Add(new JsonSchemaValidationError(JsonSchemaErrorKind.ValueNotAllowed, pointer, displayPath, "No value is allowed here."));
            }

            return;
        }

        if (schemaNode is not JsonObject schema)
        {
            return;
        }

        // A $ref points (as a root-relative JSON Pointer) to another schema the instance must also satisfy.
        // Sibling keywords still apply, so resolution does not short-circuit the checks below. visitedRefs guards
        // against a reference cycle that would otherwise recurse forever without consuming the instance; it is
        // reset whenever validation descends into a child value (see ValidateObject / ValidateArray).
        if (schema["$ref"] is JsonValue reference)
        {
            var target = reference.GetValue<string>();
            ThrowIfCircularReference(visitedRefs, target);
            ValidateNode(instance, ResolveReference(root, target), pointer, displayPath, root, visitedRefs, errors);
        }

        ValidateType(instance, schema, pointer, displayPath, errors);
        ValidateEnum(instance, schema, pointer, displayPath, errors);
        ValidateObject(instance, schema, pointer, displayPath, root, errors);
        ValidateArray(instance, schema, pointer, displayPath, root, errors);
    }

    private static void ThrowIfCircularReference(HashSet<string> visitedRefs, string reference)
    {
        if (!visitedRefs.Add(reference))
        {
            throw new ArgumentException($"The schema contains a circular $ref: '{reference}'.");
        }
    }

    private static JsonNode ResolveReference(JsonNode root, string reference)
    {
        var pointer = reference.StartsWith('#') ? reference[1..] : reference;
        var current = root;
        foreach (var rawToken in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            // RFC 6901 escaping: decode "~1" to "/" first, then "~0" to "~".
            var token = rawToken
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            var next = current switch
            {
                JsonObject obj => obj[token],
                JsonArray array when int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    && index < array.Count => array[index],
                _ => null,
            };

            current = next ?? throw new ArgumentException(
                $"The schema contains an unresolvable $ref: '{reference}'.");
        }

        return current;
    }

    private static void ValidateType(
        JsonNode? instance,
        JsonObject schema,
        string pointer,
        string displayPath,
        List<JsonSchemaValidationError> errors)
    {
        switch (schema["type"])
        {
            case JsonValue typeValue:
                var type = typeValue.GetValue<string>();
                if (!TypeMatches(instance, type))
                {
                    errors.Add(new JsonSchemaValidationError(
                        JsonSchemaErrorKind.TypeMismatch,
                        pointer,
                        displayPath,
                        $"Expected {type}, but found {ActualType(instance)}."));
                }

                break;
            case JsonArray typeArray:
                var allowedTypes = typeArray.Select(static t => t?.GetValue<string>()).ToArray();
                var matchesAny = allowedTypes.Any(t => t is not null && TypeMatches(instance, t));
                if (!matchesAny)
                {
                    var expected = string.Join(" or ", allowedTypes);
                    errors.Add(new JsonSchemaValidationError(
                        JsonSchemaErrorKind.TypeMismatch,
                        pointer,
                        displayPath,
                        $"Expected {expected}, but found {ActualType(instance)}."));
                }

                break;
        }
    }

    private static void ValidateEnum(
        JsonNode? instance,
        JsonObject schema,
        string pointer,
        string displayPath,
        List<JsonSchemaValidationError> errors)
    {
        if (schema["enum"] is not JsonArray allowed)
        {
            return;
        }

        var isAllowed = allowed.Any(candidate => JsonNode.DeepEquals(instance, candidate));
        if (isAllowed)
        {
            return;
        }

        var rendered = string.Join(", ", allowed.Select(RenderValue));
        errors.Add(new JsonSchemaValidationError(
            JsonSchemaErrorKind.DisallowedValue,
            pointer,
            displayPath,
            $"{RenderValue(instance)} is not one of the allowed values: {rendered}."));
    }

    private static void ValidateObject(
        JsonNode? instance,
        JsonObject schema,
        string pointer,
        string displayPath,
        JsonNode root,
        List<JsonSchemaValidationError> errors)
    {
        if (instance is not JsonObject obj)
        {
            return;
        }

        if (schema["required"] is JsonArray required)
        {
            foreach (var entry in required)
            {
                var name = entry?.GetValue<string>();
                if (name is not null && !obj.ContainsKey(name))
                {
                    errors.Add(new JsonSchemaValidationError(
                        JsonSchemaErrorKind.MissingProperty,
                        pointer,
                        displayPath,
                        $"Missing required property '{name}'."));
                }
            }
        }

        var properties = schema["properties"] as JsonObject;
        var additional = schema["additionalProperties"];
        foreach (var (key, value) in obj)
        {
            if (properties?[key] is { } propertySchema)
            {
                ValidateNode(value, propertySchema, Append(pointer, key), AppendKey(displayPath, key), root, [], errors);
            }
            else if (additional is JsonValue additionalValue && additionalValue.GetValueKind() is JsonValueKind.False)
            {
                // Point at the offending member itself (it exists in the instance), not the enclosing object.
                errors.Add(new JsonSchemaValidationError(
                    JsonSchemaErrorKind.UnknownProperty,
                    Append(pointer, key),
                    AppendKey(displayPath, key),
                    $"Unknown property '{key}'."));
            }
            else if (additional is JsonObject additionalSchema)
            {
                ValidateNode(value, additionalSchema, Append(pointer, key), AppendKey(displayPath, key), root, [], errors);
            }
        }
    }

    private static void ValidateArray(
        JsonNode? instance,
        JsonObject schema,
        string pointer,
        string displayPath,
        JsonNode root,
        List<JsonSchemaValidationError> errors)
    {
        if (instance is not JsonArray array)
        {
            return;
        }

        if (schema["items"] is not { } items)
        {
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var index = i.ToString(CultureInfo.InvariantCulture);
            var itemPointer = $"{pointer}/{index}";
            var itemDisplay = $"{displayPath}[{index}]";
            ValidateNode(array[i], items, itemPointer, itemDisplay, root, [], errors);
        }
    }

    private static bool TypeMatches(JsonNode? instance, string type)
    {
        var kind = instance?.GetValueKind() ?? JsonValueKind.Null;
        return type switch
        {
            "null" => kind is JsonValueKind.Null,
            "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
            "object" => kind is JsonValueKind.Object,
            "array" => kind is JsonValueKind.Array,
            "string" => kind is JsonValueKind.String,
            "number" => kind is JsonValueKind.Number,
            "integer" => kind is JsonValueKind.Number && IsIntegral(instance!),
            _ => true, // Unknown type keyword: do not fail on a constraint we do not understand.
        };
    }

    private static bool IsIntegral(JsonNode instance)
    {
        var value = instance.AsValue();
        if (value.TryGetValue<long>(out _))
        {
            return true;
        }

        return value.TryGetValue<double>(out var number) && double.IsInteger(number);
    }

    private static string ActualType(JsonNode? instance)
        => (instance?.GetValueKind() ?? JsonValueKind.Null) switch
        {
            JsonValueKind.Null => "null",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            _ => "unknown",
        };

    private static string RenderValue(JsonNode? instance)
        => instance?.ToJsonString() ?? "null";

    // Appends one object-member step to an RFC 6901 JSON Pointer, escaping "~" and "/" in the key.
    private static string Append(string pointer, string key)
    {
        var escaped = key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        return $"{pointer}/{escaped}";
    }

    // Appends one object-member step to a human-friendly display path: ".key", or just "key" at the root.
    private static string AppendKey(string displayPath, string key)
        => displayPath.Length == 0 ? key : $"{displayPath}.{key}";
}
