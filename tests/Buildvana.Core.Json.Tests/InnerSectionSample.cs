// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// The member type reached through both of KeyedRepeatedSample's sibling sections.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record InnerSectionSample
{
    public string? Text { get; init; }
}
