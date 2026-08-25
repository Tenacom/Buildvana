// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// A keyed-object list where it actually lives: as a property of an enclosing model.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated only by the deserializer in the round-trip test; never constructed directly.")]
internal sealed record KeyedHolderSample
{
    public IReadOnlyList<KeyedValueSample>? Policies { get; init; }

    public string? Name { get; init; }
}
