// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Versioning;
using Buildvana.Tool.Subcommands;

internal sealed class VersionAdvanceSettingsTests
{
    [Test]
    public async Task Parse_Defaults_ResolveToExpectedValues()
    {
        var settings = Parse([], []);
        await Assert.That(settings.ResolveChange()).IsEqualTo(VersionSpecChange.None);
        await Assert.That(settings.CheckPublicApi).IsNull();
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
        await Assert.That(Parse([], ["--check-public-api", "false"]).CheckPublicApi).IsFalse();
        await Assert.That(Parse([], ["--check-public-api=false"]).CheckPublicApi).IsFalse();
    }

    [Test]
    public async Task Parse_ReadsForce()
    {
        await Assert.That(Parse([], ["--force"]).Force).IsTrue();
    }

    [Test]
    public async Task Parse_Throws_OnInvalidBool()
    {
        await Assert.That(() => Parse([], ["--check-public-api", "maybe"])).Throws<BuildFailedException>();
    }

    private static VersionAdvanceSettings Parse(string[] positionals, string[] options)
        => VersionAdvanceSettings.Parse(positionals, options);
}
