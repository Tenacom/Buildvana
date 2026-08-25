// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// Misconfigured on purpose: key and value name the same property.
[JsonKeyedObject(nameof(Name), nameof(Name))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Only fed to the converter under test, whose configuration check rejects it before instantiation.")]
internal sealed record SameKeyValueSample
{
    public string? Name { get; init; }
}
