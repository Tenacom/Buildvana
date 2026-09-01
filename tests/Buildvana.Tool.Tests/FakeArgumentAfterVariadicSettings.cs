// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Tool.CommandLine;

/// <summary>
/// Settings double declaring a positional argument after a variadic one, violating the declaration-order
/// contract of <c>BvArgumentAttribute</c>, for <c>CommandRegistry</c> argument-order validation tests.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the argument-order validation under test; never instantiated.")]
internal sealed class FakeArgumentAfterVariadicSettings
{
    [BvArgument("[ID...]")]
    public IReadOnlyList<string> Ids { get; init; } = [];

    [BvArgument("[MODE]")]
    public string? Mode { get; init; }
}
