// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// The complex type KeyedRepeatedSample holds twice; its own complex member is what the exporter deduplicates.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record SharedSectionSample
{
    public InnerSectionSample? Inner { get; init; }
}
