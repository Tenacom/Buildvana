// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// A model with required and optional members, proving that C# required members surface as the schema's
// "required" keyword and that required strings — and only those — also gain the non-blank constraints.
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record RequiredSample
{
    public required string? Must { get; init; }

    public required bool? MustFlag { get; init; }

    public string? May { get; init; }
}
