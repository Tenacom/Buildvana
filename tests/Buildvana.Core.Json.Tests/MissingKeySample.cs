// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Core.Json;

// Misconfigured on purpose: the named key property does not exist.
[JsonKeyedObject("Nope")]
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Only fed to the converter under test, whose configuration check rejects it before instantiation.")]
internal sealed record MissingKeySample
{
    public string? Caption { get; init; }
}
