// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Json;

/// <summary>
/// Converts values of type <see cref="IReadOnlyList{T}"/>, where <c>T</c> carries
/// <see cref="JsonKeyedObjectAttribute"/>, to and from the keyed-object JSON shape the attribute describes:
/// a JSON object with one property per element, in document order.
/// </summary>
/// <remarks>
/// <para>Duplicate keys mirror <see cref="System.Text.Json"/> dictionary deserialization: under
/// <see cref="JsonSerializerOptions.AllowDuplicateProperties"/>, a key keeps its first position and takes its
/// last value; with the option off, a duplicate key fails the parse.</para>
/// <para>Elements are deserialized through the type metadata of the active
/// <see cref="JsonSerializerOptions"/>, so naming policies, converters, and unmapped-member handling apply to
/// element members unchanged, and source-generated contexts work.</para>
/// </remarks>
public sealed partial class JsonKeyedObjectConverter : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
        => TryGetElementType(typeToConvert) is { } elementType
            && elementType.IsDefined(typeof(JsonKeyedObjectAttribute), inherit: false);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Guard.IsNotNull(options);

        var elementType = TryGetElementType(typeToConvert)!;
        var attribute = elementType.GetCustomAttribute<JsonKeyedObjectAttribute>(inherit: false)!;

        var keyProperty = GetNamedProperty(elementType, attribute.KeyPropertyName, "key");
        if (keyProperty.PropertyType != typeof(string))
        {
            throw new InvalidOperationException(
                $"Property '{attribute.KeyPropertyName}' of type '{elementType}' is named as its key property "
                + $"by {nameof(JsonKeyedObjectAttribute)}, so it must be of type string, not '{keyProperty.PropertyType}'.");
        }

        var keyJsonName = GetJsonName(keyProperty, options);
        var valueJsonName = attribute.ValuePropertyName is null
            ? null
            : GetJsonName(GetNamedProperty(elementType, attribute.ValuePropertyName, "value"), options);
        var converterType = typeof(ListConverter<>).MakeGenericType(elementType);
        return (JsonConverter)Activator.CreateInstance(converterType, keyJsonName, valueJsonName)!;
    }

    private static Type? TryGetElementType(Type type)
        => type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? type.GetGenericArguments()[0]
            : null;

    private static PropertyInfo GetNamedProperty(Type elementType, string clrName, string role)
        => elementType.GetProperty(clrName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"Type '{elementType}' has no public instance property '{clrName}', "
                + $"which {nameof(JsonKeyedObjectAttribute)} names as its {role} property.");

    // Mirrors the serializer's own naming rule, like JsonSchemaGenerator.GetJsonName does for schema generation.
    private static string GetJsonName(PropertyInfo property, JsonSerializerOptions options)
        => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? options.PropertyNamingPolicy?.ConvertName(property.Name)
            ?? property.Name;
}
