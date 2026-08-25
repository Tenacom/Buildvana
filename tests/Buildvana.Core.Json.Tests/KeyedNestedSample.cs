// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// A keyed element that itself holds a keyed list, so the inner list's converter runs against the synthesized
// element document rather than the caller's reader.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated only by the deserializer in the round-trip test; never constructed directly.")]
internal sealed record KeyedNestedSample
{
    public required string Caption { get; init; }

    // ReSharper disable once UnusedAutoPropertyAccessor.Global // set only by the deserializer in the round-trip test
    public IReadOnlyList<KeyedValueSample>? Policies { get; init; }
}
