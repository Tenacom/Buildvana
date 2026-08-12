// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure;

internal sealed class ConsoleEncodingScopeTests
{
    // Pins an opt-out contract that is the .NET CLI's rather than ours: only the literal "1" leaves the console
    // alone. Deliberately unlike ConsoleReporter's NO_COLOR check, where any non-empty value counts — each
    // convention is honored on its owner's terms, so the asymmetry is the feature and must not be "fixed".
    // The trailing-space case is not hypothetical: `set VAR=1 & ...` in cmd.exe assigns "1 ", not "1".
    [Test]
    [Arguments(null, false)]
    [Arguments("", false)]
    [Arguments("1", true)]
    [Arguments("1 ", false)]
    [Arguments(" 1", false)]
    [Arguments("01", false)]
    [Arguments("0", false)]
    [Arguments("true", false)]
    [Arguments("yes", false)]
    public async Task IsDefaultEncodingRequested_AcceptsOnlyTheLiteralOne(string? variableValue, bool expected)
    {
        await Assert.That(ConsoleEncodingScope.IsDefaultEncodingRequested(variableValue)).IsEqualTo(expected);
    }
}
