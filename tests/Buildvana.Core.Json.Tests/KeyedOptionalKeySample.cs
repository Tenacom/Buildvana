// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// A keyed element whose key is not required, so the non-blank discipline does not reach its member names.
[JsonKeyedObject(nameof(Caption))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record KeyedOptionalKeySample
{
    public string Caption { get; init; } = string.Empty;

    public string? Files { get; init; }
}
