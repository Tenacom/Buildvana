// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// A model with one required and one optional member, proving that C# required members surface as the
// schema's "required" keyword.
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the schema generator under test; never instantiated.")]
internal sealed record RequiredSample
{
    public required string? Must { get; init; }

    public string? May { get; init; }
}
