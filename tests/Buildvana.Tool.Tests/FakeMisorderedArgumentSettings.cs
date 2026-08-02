// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Tool.CommandLine;

/// <summary>
/// Settings double declaring an optional positional argument before a required one, violating the
/// declaration-order contract of <c>BvArgumentAttribute</c>, for <c>CommandRegistry</c> argument-order
/// validation tests.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the argument-order validation under test; never instantiated.")]
internal sealed class FakeMisorderedArgumentSettings
{
    [BvArgument("[MODE]")]
    public string? Mode { get; init; }

    [BvArgument("<NAME>")]
    public string? Name { get; init; }
}
