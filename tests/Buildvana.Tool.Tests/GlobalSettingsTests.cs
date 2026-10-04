// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Subcommands;

internal sealed class GlobalSettingsTests
{
    [Test]
    public async Task ToTokens_WithoutOptions_IsEmpty()
    {
        var settings = new GlobalSettings(null, false, false, false, false, false, false);

        await Assert.That(settings.ToTokens()).IsEmpty();
    }

    // --version is left out: it ends a run instead of configuring one.
    [Test]
    public async Task ToTokens_RendersEveryOptionButVersion_InHelpOrder()
    {
        var settings = new GlobalSettings("detailed", true, true, true, true, true, true);

        await Assert.That(settings.ToTokens()).IsEquivalentTo(
            ["--verbosity", "detailed", "--color", "--no-color", "--nologo", "--skip-sdk-check", "--skip-delegation"]);
    }

    [Test]
    public async Task ToTokens_RoundTripsThroughTheSplitter()
    {
        var parsed = CliArgSplitter.Split(["-v", "quiet", "--nologo", "build", "--skip-sdk-check"]);

        await Assert.That(parsed.Globals.ToTokens()).IsEquivalentTo(["--verbosity", "quiet", "--nologo", "--skip-sdk-check"]);
    }
}
