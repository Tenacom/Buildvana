// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;
using Buildvana.Tool.Subcommands;

internal sealed class VersionAdvanceSettingsTests
{
    [Test]
    public async Task Parse_Defaults_ResolveToExpectedValues()
    {
        var settings = Parse([], []);
        await Assert.That(settings.ResolveChange()).IsEqualTo(VersionSpecChange.None);
        await Assert.That(settings.ResolveCheckPublicApi()).IsTrue();
        await Assert.That(settings.Force).IsFalse();
    }

    [Test]
    public async Task Parse_ReadsChangeArgument_CaseInsensitively()
    {
        await Assert.That(Parse(["minor"], []).ResolveChange()).IsEqualTo(VersionSpecChange.Minor);
        await Assert.That(Parse(["MAJOR"], []).ResolveChange()).IsEqualTo(VersionSpecChange.Major);
    }

    [Test]
    public async Task ResolveChange_Throws_OnInvalidValue()
    {
        var settings = Parse(["bogus"], []);
        await Assert.That(settings.ResolveChange).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_ReadsCheckPublicApi_SpaceAndInlineForms()
    {
        await Assert.That(Parse([], ["--check-public-api", "false"]).ResolveCheckPublicApi()).IsFalse();
        await Assert.That(Parse([], ["--check-public-api=false"]).ResolveCheckPublicApi()).IsFalse();
    }

    [Test]
    public async Task Parse_ReadsForce()
    {
        await Assert.That(Parse([], ["--force"]).Force).IsTrue();
    }

    [Test]
    public async Task ResolveCheckPublicApi_ReadsReleaseConfig_WhenFlagAbsent()
    {
        var config = new BuildvanaConfig { Release = new() { CheckPublicApi = false } };
        await Assert.That(Parse([], [], config).ResolveCheckPublicApi()).IsFalse();
    }

    [Test]
    public async Task ResolveCheckPublicApi_FlagWins_OverReleaseConfig()
    {
        var config = new BuildvanaConfig { Release = new() { CheckPublicApi = false } };
        await Assert.That(Parse([], ["--check-public-api", "true"], config).ResolveCheckPublicApi()).IsTrue();
    }

    [Test]
    public async Task Parse_Throws_OnInvalidBool()
    {
        await Assert.That(() => Parse([], ["--check-public-api", "maybe"])).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_Throws_OnUnknownOption()
    {
        await Assert.That(() => Parse([], ["--bogus"])).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_Throws_OnExcessPositionals()
    {
        await Assert.That(() => Parse(["minor", "extra"], [])).Throws<BuildFailedException>();
    }

    private static VersionAdvanceSettings Parse(string[] positionals, string[] options, BuildvanaConfig? config = null)
        => VersionAdvanceSettings.Parse(positionals, options, config ?? new BuildvanaConfig());
}
