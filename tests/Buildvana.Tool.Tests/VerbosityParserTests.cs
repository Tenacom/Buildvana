// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Tool.CommandLine;

internal sealed class VerbosityParserTests
{
    // Pins the CLI contract for --verbosity: full names and short aliases, case-insensitively.
    [Test]
    [Arguments("quiet", Verbosity.Quiet)]
    [Arguments("q", Verbosity.Quiet)]
    [Arguments("minimal", Verbosity.Minimal)]
    [Arguments("m", Verbosity.Minimal)]
    [Arguments("normal", Verbosity.Normal)]
    [Arguments("n", Verbosity.Normal)]
    [Arguments("detailed", Verbosity.Detailed)]
    [Arguments("d", Verbosity.Detailed)]
    [Arguments("diagnostic", Verbosity.Diagnostic)]
    [Arguments("diag", Verbosity.Diagnostic)]
    [Arguments("QUIET", Verbosity.Quiet)]
    [Arguments("Diag", Verbosity.Diagnostic)]
    public async Task Parse_KnownLevel_ReturnsVerbosity(string raw, Verbosity expected)
    {
        await Assert.That(VerbosityParser.Parse(raw)).IsEqualTo(expected);
    }

    [Test]
    public async Task Parse_UnknownLevel_FailsNamingValueAndAlternatives()
    {
        var exception = await Assert.That(() => VerbosityParser.Parse("loud")).Throws<BuildFailedException>();
        await Assert.That(exception!.Message)
            .IsEqualTo("Unknown verbosity level 'loud'. Use one of: [q]uiet, [m]inimal, [n]ormal, [d]etailed, [diag]nostic.");
    }
}
