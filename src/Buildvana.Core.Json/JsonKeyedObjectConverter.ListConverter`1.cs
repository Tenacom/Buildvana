// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Buildvana.Core.Json;

partial class JsonKeyedObjectConverter
{
    // The strategy on both sides is to go through a per-element JSON document in the ordinary property shape,
    // synthesized on read and serialized on write, so that the element type's own serialization contract
    // (naming, converters, required members, unmapped-member and duplicate handling) applies unchanged.
    // JsonElement, not JsonNode: JsonObject cannot represent duplicate members, and materializing one throws
    // ArgumentException, while raw elements pass duplicates through for the serializer to settle.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated via Activator.CreateInstance on a constructed generic type, invisible to the analyzer.")]
    private sealed class ListConverter<T>(
        string keyPropertyName,
        string? valuePropertyName) : JsonConverter<IReadOnlyList<T>>
    {
        // volatile: GetElementTypeInfo publishes the two name fields behind a guard on this one, and a shared
        // JsonSerializerOptions (documented as safe for concurrent use) caches this converter instance. The
        // release store orders the name writes before publication; the acquire load keeps reads below it.
        private volatile JsonTypeInfo<T>? _elementTypeInfo;
        private string? _keyJsonName;
        private string? _valueJsonName;

        public override IReadOnlyList<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var typeInfo = GetElementTypeInfo(options);
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected a JSON object, found {reader.TokenType}.");
            }

            var documents = new List<JsonDocument>();
            try
            {
                var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
                var entries = new List<(string Key, JsonElement Value)>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    var key = reader.GetString()!;
                    _ = reader.Read();
                    var document = JsonDocument.ParseValue(ref reader);
                    documents.Add(document);
                    var entry = (key, document.RootElement);
                    if (indexByKey.TryGetValue(key, out var index))
                    {
                        if (!options.AllowDuplicateProperties)
                        {
                            throw new JsonException($"Duplicate key '{key}'.");
                        }

                        // Mirror dictionary deserialization: the key keeps its first position and takes its last value.
                        entries[index] = entry;
                    }
                    else
                    {
                        indexByKey.Add(key, entries.Count);
                        entries.Add(entry);
                    }
                }

                // Unreachable through JsonSerializer, whose read-ahead hands converters complete values; without
                // the guard, truncated input would return a partial list instead of failing.
                if (reader.TokenType != JsonTokenType.EndObject)
                {
                    throw new JsonException("Expected the end of a JSON object.");
                }

                var buffer = new ArrayBufferWriter<byte>();
                using var elementWriter = new Utf8JsonWriter(buffer);
                var result = new List<T>(entries.Count);
                foreach (var (key, value) in entries)
                {
                    buffer.ResetWrittenCount();
                    elementWriter.Reset(buffer);
                    WriteElementJson(elementWriter, key, value);
                    result.Add(JsonSerializer.Deserialize(buffer.WrittenSpan, typeInfo)!);
                }

                return result;
            }
            finally
            {
                foreach (var document in documents)
                {
                    document.Dispose();
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
        {
            var typeInfo = GetElementTypeInfo(options);
            writer.WriteStartObject();
            var writtenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in value)
            {
                var elementJson = JsonSerializer.SerializeToElement(element, typeInfo);
                if (elementJson.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException($"A list element serialized as {elementJson.ValueKind}, not as a JSON object.");
                }

                WriteElement(writer, elementJson, writtenKeys);
            }

            writer.WriteEndObject();
        }

        private static JsonPropertyInfo GetNamedProperty(JsonTypeInfo<T> typeInfo, string clrName, string role)
        {
            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is MemberInfo { Name: var name } && name == clrName)
                {
                    return property;
                }
            }

            throw new InvalidOperationException(
                $"Type '{typeof(T)}' has no serializable property '{clrName}', "
                + $"which {nameof(JsonKeyedObjectAttribute)} names as its {role} property.");
        }

        private void WriteElementJson(Utf8JsonWriter writer, string key, JsonElement value)
        {
            writer.WriteStartObject();
            writer.WriteString(_keyJsonName!, key);
            if (valuePropertyName is not null)
            {
                writer.WritePropertyName(_valueJsonName!);
                value.WriteTo(writer);
            }
            else
            {
                if (value.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException($"The value of '{key}' must be a JSON object.");
                }

                foreach (var member in value.EnumerateObject())
                {
                    if (member.NameEquals(_keyJsonName))
                    {
                        throw new JsonException($"The value of '{key}' must not state the key property '{_keyJsonName}'.");
                    }

                    member.WriteTo(writer);
                }
            }

            writer.WriteEndObject();

            // The writer is reused across elements and disposed only after the last one, so the flush that
            // disposal used to provide has to be explicit before the caller reads the buffer.
            writer.Flush();
        }

        private void WriteElement(Utf8JsonWriter writer, JsonElement element, HashSet<string> writtenKeys)
        {
            if (!element.TryGetProperty(_keyJsonName!, out var keyElement) || keyElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"The key property '{_keyJsonName}' must serialize as a non-null string.");
            }

            // Utf8JsonWriter validates structure only, so without this check two elements sharing a key would
            // produce a document this converter's own Read refuses or silently collapses.
            var key = keyElement.GetString()!;
            if (!writtenKeys.Add(key))
            {
                throw new JsonException($"Duplicate key '{key}'.");
            }

            writer.WritePropertyName(key);
            if (valuePropertyName is not null)
            {
                WriteValueProperty(writer, element);
            }
            else
            {
                writer.WriteStartObject();
                foreach (var member in element.EnumerateObject())
                {
                    if (!member.NameEquals(_keyJsonName))
                    {
                        member.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }
        }

        private void WriteValueProperty(Utf8JsonWriter writer, JsonElement element)
        {
            if (element.TryGetProperty(_valueJsonName!, out var valueElement))
            {
                valueElement.WriteTo(writer);
            }
            else
            {
                // An absent value property (e.g. ignored when null) writes as JSON null. Reading that back
                // restores the absence only where reading null is legal: options that respect nullable
                // annotations, or a value-typed value property, reject it.
                writer.WriteNullValue();
            }
        }

        // Also resolves the attribute's CLR property names against the type info's own properties: their Name
        // is the JSON name the serializer will actually use, whatever naming policy, TypeInfoResolver
        // (source-generated contexts included), or contract modifier produced it.
        private JsonTypeInfo<T> GetElementTypeInfo(JsonSerializerOptions options)
        {
            if (_elementTypeInfo is null)
            {
                var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
                var keyProperty = GetNamedProperty(typeInfo, keyPropertyName, "key");
                if (keyProperty.PropertyType != typeof(string))
                {
                    throw new InvalidOperationException(
                        $"Property '{keyPropertyName}' of type '{typeof(T)}' is named as its key property "
                        + $"by {nameof(JsonKeyedObjectAttribute)}, so it must be of type string, not '{keyProperty.PropertyType}'.");
                }

                _keyJsonName = keyProperty.Name;
                _valueJsonName = valuePropertyName is null ? null : GetNamedProperty(typeInfo, valuePropertyName, "value").Name;
                // A null _valueJsonName never equals _keyJsonName, which is non-null by this point.
                if (_valueJsonName == _keyJsonName)
                {
                    throw new InvalidOperationException(
                        $"{nameof(JsonKeyedObjectAttribute)} on type '{typeof(T)}' resolves its key and value properties "
                        + $"to the same JSON name '{_keyJsonName}'.");
                }

                _elementTypeInfo = typeInfo;
            }

            return _elementTypeInfo;
        }
    }
}
