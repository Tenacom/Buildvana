// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// An element type referencing itself through a plain property: the exporter renders that as a "$ref"
// pointer, which cannot be embedded as a subschema, so schema generation must refuse it.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record KeyedRecursiveSample
{
    public required string Caption { get; init; }

    public KeyedRecursiveSample? Child { get; init; }
}
