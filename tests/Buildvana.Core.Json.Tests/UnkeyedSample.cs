// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// Carries no JsonKeyedObjectAttribute, so lists of it must stay outside the converter's reach.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Only inspected by CanConvert tests; never (de)serialized.")]
internal sealed record UnkeyedSample
{
    public string? Name { get; init; }
}
