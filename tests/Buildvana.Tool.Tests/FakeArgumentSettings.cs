// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Buildvana.Tool.CommandLine;

/// <summary>
/// Settings double declaring one required positional argument, for <c>CommandArgumentValidator</c> tests:
/// no real command declares a required argument.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Reflected over by the argument validator under test; never instantiated.")]
internal sealed class FakeArgumentSettings
{
    [BvArgument("<NAME>")]
    public string? Name { get; init; }
}
