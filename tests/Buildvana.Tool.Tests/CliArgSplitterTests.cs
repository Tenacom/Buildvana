// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.CommandLine;

internal sealed class CliArgSplitterTests
{
    [Test]
    public async Task Split_RecognizesBareSubcommand()
    {
        var result = CliArgSplitter.Split(["build"]);
        await Assert.That(result.Subcommand).IsEqualTo("build");
        await Assert.That(result.HelpRequested).IsFalse();
        await Assert.That(result.Positionals.Count).IsEqualTo(0);
        await Assert.That(result.OptionTokens.Count).IsEqualTo(0);
        await Assert.That(result.Forwarded.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Split_ForwardsTokensAfterSeparatorVerbatim()
    {
        var result = CliArgSplitter.Split(["build", "--", "-p:Foo=Bar", "-m:8"]);
        await Assert.That(result.Subcommand).IsEqualTo("build");
        await Assert.That(result.OptionTokens.Count).IsEqualTo(0);
        await Assert.That(Join(result.Forwarded)).IsEqualTo("-p:Foo=Bar|-m:8");
    }

    [Test]
    public async Task Split_StripsValueGlobalBeforeSubcommandDetection()
    {
        // The loophole case: `detailed` is the value of --verbosity, not the subcommand.
        var result = CliArgSplitter.Split(["--verbosity", "detailed", "build"]);
        await Assert.That(result.Globals.Verbosity).IsEqualTo("detailed");
        await Assert.That(result.Subcommand).IsEqualTo("build");
        await Assert.That(result.Positionals.Count).IsEqualTo(0);
        await Assert.That(result.OptionTokens.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Split_StripsFlagGlobalsOnEitherSideOfSubcommand()
    {
        var result = CliArgSplitter.Split(["--nologo", "build", "--color"]);
        await Assert.That(result.Subcommand).IsEqualTo("build");
        await Assert.That(result.Globals.Nologo).IsTrue();
        await Assert.That(result.Globals.Color).IsTrue();
        await Assert.That(result.OptionTokens.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Split_InlineGlobalDoesNotConsumeSubcommand()
    {
        var result = CliArgSplitter.Split(["--verbosity=detailed", "build"]);
        await Assert.That(result.Globals.Verbosity).IsEqualTo("detailed");
        await Assert.That(result.Subcommand).IsEqualTo("build");
    }

    [Test]
    public async Task Split_RecognizesHelpWithoutSubcommand()
    {
        var result = CliArgSplitter.Split(["--help"]);
        await Assert.That(result.HelpRequested).IsTrue();
        await Assert.That(result.Subcommand).IsNull();
    }

    [Test]
    public async Task Split_RecognizesShortHelpAfterSubcommand()
    {
        var result = CliArgSplitter.Split(["build", "-h"]);
        await Assert.That(result.Subcommand).IsEqualTo("build");
        await Assert.That(result.HelpRequested).IsTrue();
    }

    [Test]
    public async Task Split_RecognizesVersionFlag()
    {
        var result = CliArgSplitter.Split(["--version"]);
        await Assert.That(result.Globals.Version).IsTrue();
        await Assert.That(result.Subcommand).IsNull();
    }

    [Test]
    public async Task Split_LeavesCommandOptionsAfterSubcommandInOptionTokens()
    {
        var result = CliArgSplitter.Split(["release", "-c", "Debug", "--bump", "minor"]);
        await Assert.That(result.Subcommand).IsEqualTo("release");
        await Assert.That(result.Positionals.Count).IsEqualTo(0);
        await Assert.That(Join(result.OptionTokens)).IsEqualTo("-c|Debug|--bump|minor");
    }

    [Test]
    public async Task Split_CollectsPositionalsUntilFirstOption()
    {
        var result = CliArgSplitter.Split(["just", "build", "-c", "Debug"]);
        await Assert.That(result.Subcommand).IsEqualTo("just");
        await Assert.That(Join(result.Positionals)).IsEqualTo("build");
        await Assert.That(Join(result.OptionTokens)).IsEqualTo("-c|Debug");
    }

    [Test]
    public async Task Split_ReturnsNullSubcommand_WhenOnlyGlobalsGiven()
    {
        var result = CliArgSplitter.Split(["--nologo"]);
        await Assert.That(result.Subcommand).IsNull();
        await Assert.That(result.Globals.Nologo).IsTrue();
        await Assert.That(result.OptionTokens.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Split_GlobalsAfterSeparatorAreForwardedNotParsed()
    {
        var result = CliArgSplitter.Split(["build", "--", "--nologo", "--verbosity", "quiet"]);
        await Assert.That(result.Globals.Nologo).IsFalse();
        await Assert.That(result.Globals.Verbosity).IsNull();
        await Assert.That(Join(result.Forwarded)).IsEqualTo("--nologo|--verbosity|quiet");
    }

    [Test]
    public async Task Split_Throws_WhenValueGlobalHasNoValue() => await Assert.That(
            () => CliArgSplitter.Split(["build", "--verbosity"]))
                .Throws<BuildFailedException>();

    private static string Join(IReadOnlyList<string> tokens) => string.Join('|', tokens);
}
