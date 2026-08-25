// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// Two sibling properties of one complex type that itself has a complex member: the exporter reaches the
// inner member's (type info, property info) pair twice and deduplicates the second visit into a "$ref"
// pointer, without any recursion in the model.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record KeyedRepeatedSample
{
    public required string Caption { get; init; }

    public SharedSectionSample? First { get; init; }

    public SharedSectionSample? Second { get; init; }
}
