// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// An element type reaching a "$ref" through an "anyOf" the exporter emits for polymorphic hierarchies: the
// recursion guard must descend into "anyOf" to catch it.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record KeyedPolymorphicSample
{
    public required string Caption { get; init; }

    public PolymorphicNodeSample? Node { get; init; }
}
