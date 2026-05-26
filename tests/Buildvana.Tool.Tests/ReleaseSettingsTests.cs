// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.Commands;
using Buildvana.Tool.Services.Versioning;

internal sealed class ReleaseSettingsTests
{
    [Test]
    public async Task Parse_Defaults_ResolveToExpectedValues()
    {
        var settings = ReleaseSettings.Parse([]);
        await Assert.That(settings.ResolveConfiguration()).IsEqualTo("Release");
        await Assert.That(settings.ResolveBump()).IsEqualTo(VersionSpecChange.None);
        await Assert.That(settings.ResolveCheckPublicApi()).IsTrue();
        await Assert.That(settings.ResolveUnstableChangelog()).IsFalse();
        await Assert.That(settings.ResolveRequireChangelog()).IsTrue();
        await Assert.That(settings.ResolveDogfood()).IsTrue();
    }

    [Test]
    public async Task Parse_ReadsConfiguration_ShortAndInlineForms()
    {
        await Assert.That(ReleaseSettings.Parse(["-c", "Debug"]).ResolveConfiguration()).IsEqualTo("Debug");
        await Assert.That(ReleaseSettings.Parse(["--configuration=Debug"]).ResolveConfiguration()).IsEqualTo("Debug");
    }

    [Test]
    public async Task Parse_ReadsBumpEnum()
    {
        await Assert.That(ReleaseSettings.Parse(["--bump", "minor"]).ResolveBump()).IsEqualTo(VersionSpecChange.Minor);
    }

    [Test]
    public async Task ResolveBump_Throws_OnInvalidValue()
    {
        var settings = ReleaseSettings.Parse(["--bump", "bogus"]);
        await Assert.That(settings.ResolveBump).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_ReadsBoolOptions_SpaceAndInlineForms()
    {
        var settings = ReleaseSettings.Parse(["--check-public-api", "false", "--dogfood=false"]);
        await Assert.That(settings.ResolveCheckPublicApi()).IsFalse();
        await Assert.That(settings.ResolveDogfood()).IsFalse();
    }

    [Test]
    public async Task Parse_Throws_OnInvalidBool()
    {
        await Assert.That(() => ReleaseSettings.Parse(["--dogfood", "maybe"])).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_Throws_OnUnknownOption()
    {
        await Assert.That(() => ReleaseSettings.Parse(["--bogus"])).Throws<BuildFailedException>();
    }
}
