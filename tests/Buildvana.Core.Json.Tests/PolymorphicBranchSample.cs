// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// A derived type referencing the hierarchy's base: recursion the exporter renders as a "$ref" pointer inside
// an "anyOf" branch.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record PolymorphicBranchSample : PolymorphicNodeSample
{
    public PolymorphicNodeSample? Child { get; init; }
}
