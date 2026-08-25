// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using Buildvana.Core.Json;

// The attribute names CLR properties; the converter must resolve JsonPropertyName when synthesizing JSON.
// The value property doubles as the non-string-value case.
[JsonKeyedObject(nameof(Id), nameof(Value))]
internal sealed record RenamedKeySample
{
    [JsonPropertyName("identifier")]
    public required string Id { get; init; }

    public int Value { get; init; }
}
