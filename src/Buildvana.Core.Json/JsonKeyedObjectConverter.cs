// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        => TryGetKeyedElementType(typeToConvert) is not null;

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = TryGetElementType(typeToConvert)!;
        var attribute = elementType.GetCustomAttribute<JsonKeyedObjectAttribute>(inherit: false)!;

        // The attribute's CLR property names are resolved to JSON names by the converter itself, at first use,
        // from the element's JsonTypeInfo: the serializer's own metadata is right by construction under any
        // naming policy, TypeInfoResolver, or contract customization. Recomputing the names here from
        // reflection would silently disagree the moment a contract modifier renames a property.
        var converterType = typeof(ListConverter<>).MakeGenericType(elementType);
        return (JsonConverter)Activator.CreateInstance(converterType, attribute.KeyPropertyName, attribute.ValuePropertyName)!;
    }

    // Also serves JsonSchemaGenerator, which renders a keyed list from the same detection this factory
    // converts it by, so the parser and the schema cannot drift apart.
    internal static Type? TryGetKeyedElementType(Type type)
        => TryGetElementType(type) is { } elementType && elementType.IsDefined(typeof(JsonKeyedObjectAttribute), inherit: false)
            ? elementType
            : null;

    // Resolves one of the attribute's CLR property names against a type info's own properties: their Name is
    // the JSON name the serializer will actually use, whatever naming policy, TypeInfoResolver
    // (source-generated contexts included), or contract modifier produced it. Shared between ListConverter
    // and JsonSchemaGenerator so both sides resolve names from one source.
    internal static JsonPropertyInfo GetNamedProperty(JsonTypeInfo typeInfo, string clrName, string role)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is MemberInfo { Name: var name } && name == clrName)
            {
                return property;
            }
        }

        throw new InvalidOperationException(
            $"Type '{typeInfo.Type}' has no serializable property '{clrName}', "
            + $"which {nameof(JsonKeyedObjectAttribute)} names as its {role} property.");
    }

    private static Type? TryGetElementType(Type type)
        => type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? type.GetGenericArguments()[0]
            : null;
}
