// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security;
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

    // Pins the classification both of the scope's catch filters share: which exceptions mean the console cannot
    // be reconfigured — swallowed, leaving the encoding alone — and which mean the call itself was wrong, and
    // must escape. The distinction is invisible to the compiler, so a widening that looks harmless fails here
    // instead: notably Exception.IsIORelatedException, the repo's existing classification, which admits
    // ArgumentException to account for malformed paths that no call in that class can produce.
    [Test]
    [Arguments(typeof(IOException), true)]
    [Arguments(typeof(FileNotFoundException), true)]
    [Arguments(typeof(UnauthorizedAccessException), true)]
    [Arguments(typeof(SecurityException), true)]
    [Arguments(typeof(NotSupportedException), true)]
    [Arguments(typeof(PlatformNotSupportedException), true)]
    [Arguments(typeof(OperationCanceledException), true)]
    [Arguments(typeof(ArgumentException), false)]
    [Arguments(typeof(ArgumentNullException), false)]
    [Arguments(typeof(InvalidOperationException), false)]
    public async Task IsConsoleEncodingFailure_AcceptsOnlyFailuresOfTheConsole(Type exceptionType, bool expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;
        await Assert.That(ConsoleEncodingScope.IsConsoleEncodingFailure(exception)).IsEqualTo(expected);
    }
}
