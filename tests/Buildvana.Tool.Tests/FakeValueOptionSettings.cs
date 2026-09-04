// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Tool.CommandLine;

/// <summary>
/// Settings double declaring a value option, a flag, and a variadic positional argument, for
/// <c>CommandArgumentValidator</c> tests: no real command declares both a value option and an argument, so no
/// real command can be given an operand repeating the text of a value it consumed.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the argument validator under test; never instantiated.")]
internal sealed class FakeValueOptionSettings
{
    [BvArgument("[ID...]")]
    public IReadOnlyList<string> Ids { get; init; } = [];

    [BvOption("--to <VERSION>")]
    public string? To { get; init; }

    [BvOption("--force")]
    public bool Force { get; init; }
}
