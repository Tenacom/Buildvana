// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Tool.CommandLine;

/// <summary>
/// Settings double declaring an optional positional argument followed by a variadic one, for
/// <c>CommandArgumentValidator</c> and <c>CommandRegistry</c> argument tests.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the argument validation under test; never instantiated.")]
internal sealed class FakeVariadicArgumentSettings
{
    [BvArgument("[MODE]")]
    public string? Mode { get; init; }

    [BvArgument("[ID...]")]
    public IReadOnlyList<string> Ids { get; init; } = [];
}
