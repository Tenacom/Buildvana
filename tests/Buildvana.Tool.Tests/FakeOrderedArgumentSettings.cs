// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Tool.CommandLine;

/// <summary>
/// Settings double declaring a required positional argument before an optional one, honoring the
/// declaration-order contract of <c>BvArgumentAttribute</c>, for <c>CommandRegistry</c> argument-order
/// validation tests.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the argument-order validation under test; never instantiated.")]
internal sealed class FakeOrderedArgumentSettings
{
    [BvArgument("<NAME>")]
    public string? Name { get; init; }

    [BvArgument("[MODE]")]
    public string? Mode { get; init; }
}
