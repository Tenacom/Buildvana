// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;
using Buildvana.Core.Json.Schema;

// A keyed-object element carrying an example on each side: the key's belongs in propertyNames, the value's in
// additionalProperties.
[JsonKeyedObject(nameof(Pattern), nameof(Policy))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record ExampleKeyedSample
{
    [JsonSchemaExample("\"Some.Package.*\"")]
    public required string Pattern { get; init; }

    [JsonSchemaExample("\"patch\"")]
    public required string Policy { get; init; }
}
