// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// An element type holding a keyed list of itself: schema generation must refuse the cycle instead of
// recursing forever.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record KeyedCycleSample
{
    public required string Caption { get; init; }

    public IReadOnlyList<KeyedCycleSample>? Children { get; init; }
}
