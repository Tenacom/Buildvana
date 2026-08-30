// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;
using Buildvana.Core.Json.Schema;

// A keyed element whose key is not required but carries an example, so propertyNames exists for the example
// alone.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record ExampleOptionalKeySample
{
    [JsonSchemaExample("\"SDK package injections\"")]
    public string Caption { get; init; } = string.Empty;

    public string? Files { get; init; }
}
