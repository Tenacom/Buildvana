// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Versioning;
using Buildvana.Tool.Subcommands;

internal sealed class ReleaseSettingsTests
{
    [Test]
    public async Task Parse_NoTokens_LeavesFlagsUnset()
    {
        var settings = ReleaseSettings.Parse([]);
        await Assert.That(settings.Configuration).IsNull();
        await Assert.That(settings.Bump).IsNull();
        await Assert.That(settings.CheckPublicApi).IsNull();
        await Assert.That(settings.Dogfood).IsNull();
        await Assert.That(settings.ResolveBump()).IsEqualTo(VersionSpecChange.None);
    }

    [Test]
    public async Task Parse_ReadsConfiguration_ShortAndInlineForms()
    {
        await Assert.That(ReleaseSettings.Parse(["-c", "Debug"]).Configuration).IsEqualTo("Debug");
        await Assert.That(ReleaseSettings.Parse(["--configuration=Debug"]).Configuration).IsEqualTo("Debug");
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
        await Assert.That(settings.CheckPublicApi).IsFalse();
        await Assert.That(settings.Dogfood).IsFalse();
    }

    [Test]
    public async Task Parse_Throws_OnInvalidBool()
    {
        await Assert.That(() => ReleaseSettings.Parse(["--dogfood", "maybe"])).Throws<BuildFailedException>();
    }
}
