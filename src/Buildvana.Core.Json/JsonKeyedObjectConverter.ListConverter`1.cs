// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private sealed class ListConverter<T>(string keyJsonName, string? valueJsonName) : JsonConverter<IReadOnlyList<T>>
    {
        private JsonTypeInfo<T>? _elementTypeInfo;

        public override IReadOnlyList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
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

                var typeInfo = GetElementTypeInfo(options);
                var buffer = new ArrayBufferWriter<byte>();
                var result = new List<T>(entries.Count);
                foreach (var (key, value) in entries)
                {
                    buffer.ResetWrittenCount();
                    WriteElementJson(buffer, key, value);
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
            foreach (var element in value)
            {
                var elementJson = JsonSerializer.SerializeToElement(element, typeInfo);
                if (elementJson.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException($"A list element serialized as {elementJson.ValueKind}, not as a JSON object.");
                }

                WriteElement(writer, elementJson);
            }

            writer.WriteEndObject();
        }

        private void WriteElementJson(ArrayBufferWriter<byte> buffer, string key, JsonElement value)
        {
            using var writer = new Utf8JsonWriter(buffer);
            writer.WriteStartObject();
            writer.WriteString(keyJsonName, key);
            if (valueJsonName is not null)
            {
                writer.WritePropertyName(valueJsonName);
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
                    if (member.NameEquals(keyJsonName))
                    {
                        throw new JsonException($"The value of '{key}' must not state the key property '{keyJsonName}'.");
                    }

                    member.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        private void WriteElement(Utf8JsonWriter writer, JsonElement element)
        {
            if (!element.TryGetProperty(keyJsonName, out var keyElement) || keyElement.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"The key property '{keyJsonName}' must serialize as a non-null string.");
            }

            writer.WritePropertyName(keyElement.GetString()!);
            if (valueJsonName is not null)
            {
                WriteValueProperty(writer, element);
            }
            else
            {
                writer.WriteStartObject();
                foreach (var member in element.EnumerateObject())
                {
                    if (!member.NameEquals(keyJsonName))
                    {
                        member.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }
        }

        private void WriteValueProperty(Utf8JsonWriter writer, JsonElement element)
        {
            if (element.TryGetProperty(valueJsonName!, out var valueElement))
            {
                valueElement.WriteTo(writer);
            }
            else
            {
                // An absent value property (e.g. ignored when null) writes as JSON null, so that reading it back
                // restores the same absence.
                writer.WriteNullValue();
            }
        }

        private JsonTypeInfo<T> GetElementTypeInfo(JsonSerializerOptions options)
            => _elementTypeInfo ??= (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    }
}
