// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// Misconfigured on purpose: the key property is not a string.
[JsonKeyedObject(nameof(Number))]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Only fed to the converter and schema generator under test, whose shared configuration check rejects it.")]
internal sealed record NonStringKeySample
{
    public int Number { get; init; }
}
