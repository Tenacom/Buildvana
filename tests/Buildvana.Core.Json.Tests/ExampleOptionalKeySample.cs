// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;
using Buildvana.Core.Json.Schema;

// A keyed element whose key is not required but carries a description and an example, so propertyNames exists
// for those alone.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record ExampleOptionalKeySample
{
    [JsonSchemaExample("\"SDK package injections\"")]
    [Description("Caption naming the group.")]
    public string Caption { get; init; } = string.Empty;

    public string? Files { get; init; }
}
