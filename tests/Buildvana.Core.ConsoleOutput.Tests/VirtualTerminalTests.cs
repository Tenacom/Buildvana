// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;

internal sealed class VirtualTerminalTests
{
    // Pins the TERM interpretation contract: unset, empty, and "dumb" signal a terminal that does not understand
    // escape sequences. The comparison is case-sensitive on purpose ("Dumb" passes), as terminal names are.
    [Test]
    [Arguments(null, false)]
    [Arguments("", false)]
    [Arguments("dumb", false)]
    [Arguments("Dumb", true)]
    [Arguments("xterm-256color", true)]
    public async Task IsNonWindowsTerminalCapable_ReflectsTermValue(string? term, bool expected)
    {
        await Assert.That(VirtualTerminal.IsNonWindowsTerminalCapable(term)).IsEqualTo(expected);
    }
}
