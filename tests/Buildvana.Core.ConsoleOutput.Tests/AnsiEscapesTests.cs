// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;

internal sealed class AnsiEscapesTests
{
    // The expected sequences are transcribed literally from the SGR standard (the same mapping the BCL uses
    // on Unix); the implementation computes them instead. The two derive the mapping independently, so this
    // table is the test's oracle, not a restatement of the code — keep it literal and complete.
    [Test]
    [Arguments(ConsoleColor.Black, "\e[30m")]
    [Arguments(ConsoleColor.DarkRed, "\e[31m")]
    [Arguments(ConsoleColor.DarkGreen, "\e[32m")]
    [Arguments(ConsoleColor.DarkYellow, "\e[33m")]
    [Arguments(ConsoleColor.DarkBlue, "\e[34m")]
    [Arguments(ConsoleColor.DarkMagenta, "\e[35m")]
    [Arguments(ConsoleColor.DarkCyan, "\e[36m")]
    [Arguments(ConsoleColor.Gray, "\e[37m")]
    [Arguments(ConsoleColor.DarkGray, "\e[90m")]
    [Arguments(ConsoleColor.Red, "\e[91m")]
    [Arguments(ConsoleColor.Green, "\e[92m")]
    [Arguments(ConsoleColor.Yellow, "\e[93m")]
    [Arguments(ConsoleColor.Blue, "\e[94m")]
    [Arguments(ConsoleColor.Magenta, "\e[95m")]
    [Arguments(ConsoleColor.Cyan, "\e[96m")]
    [Arguments(ConsoleColor.White, "\e[97m")]
    public async Task Foreground_KnownColor_ReturnsBclParityEscape(ConsoleColor color, string expected)
    {
        await Assert.That(AnsiEscapes.Foreground(color)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(16)]
    public async Task Foreground_OutOfRangeColor_Throws(int color)
    {
        await Assert.That(() => AnsiEscapes.Foreground((ConsoleColor)color)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Reset_IsSgr0()
    {
        await Assert.That(AnsiEscapes.Reset).IsEqualTo("\e[0m");
    }
}
