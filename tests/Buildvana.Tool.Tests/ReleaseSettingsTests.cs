// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;

internal sealed class ReleaseSettingsTests
{
    [Test]
    public async Task Parse_Defaults_ResolveToExpectedValues()
    {
        var settings = Parse([]);
        await Assert.That(settings.ResolveConfiguration()).IsEqualTo("Release");
        await Assert.That(settings.ResolveBump()).IsEqualTo(VersionSpecChange.None);
        await Assert.That(settings.ResolveCheckPublicApi()).IsTrue();
        await Assert.That(settings.ResolveChangelogUpdates()).IsEqualTo(ChangelogUpdates.Stable);
        await Assert.That(settings.ResolveEmptyChangelog()).IsNull();
        await Assert.That(settings.ResolveDogfood()).IsTrue();
    }

    [Test]
    public async Task Parse_ReadsConfiguration_ShortAndInlineForms()
    {
        await Assert.That(Parse(["-c", "Debug"]).ResolveConfiguration()).IsEqualTo("Debug");
        await Assert.That(Parse(["--configuration=Debug"]).ResolveConfiguration()).IsEqualTo("Debug");
    }

    [Test]
    public async Task ResolveConfiguration_FollowsFlagOverReleaseOverDotNetChain()
    {
        var config = new BuildvanaConfig
        {
            DotNet = new() { Configuration = "DotNetConfig" },
            Release = new() { Configuration = "ReleaseConfig" },
        };

        // Flag wins over both config sections.
        await Assert.That(Parse(["-c", "FlagConfig"], config).ResolveConfiguration()).IsEqualTo("FlagConfig");

        // With no flag, release.configuration wins over dotnet.configuration.
        await Assert.That(Parse([], config).ResolveConfiguration()).IsEqualTo("ReleaseConfig");

        // With neither flag nor release.configuration, dotnet.configuration is used.
        var dotNetOnly = new BuildvanaConfig { DotNet = new() { Configuration = "DotNetConfig" } };
        await Assert.That(Parse([], dotNetOnly).ResolveConfiguration()).IsEqualTo("DotNetConfig");
    }

    [Test]
    public async Task Resolve_ReadsReleaseConfig_WhenFlagsAbsent()
    {
        var config = new BuildvanaConfig
        {
            Release = new()
            {
                CheckPublicApi = false,
                Dogfood = false,
                ChangelogUpdates = ChangelogUpdates.All,
                EmptyChangelog = "Nothing to see here.",
            },
        };

        var settings = Parse([], config);
        await Assert.That(settings.ResolveCheckPublicApi()).IsFalse();
        await Assert.That(settings.ResolveDogfood()).IsFalse();
        await Assert.That(settings.ResolveChangelogUpdates()).IsEqualTo(ChangelogUpdates.All);
        await Assert.That(settings.ResolveEmptyChangelog()).IsEqualTo("Nothing to see here.");
    }

    [Test]
    public async Task Resolve_FlagsWin_OverReleaseConfig()
    {
        var config = new BuildvanaConfig { Release = new() { CheckPublicApi = false, Dogfood = false } };
        var settings = Parse(["--check-public-api", "true", "--dogfood=true"], config);
        await Assert.That(settings.ResolveCheckPublicApi()).IsTrue();
        await Assert.That(settings.ResolveDogfood()).IsTrue();
    }

    [Test]
    public async Task MatchesDocsBranch_DefaultsToMainAndMaster()
    {
        var settings = Parse([]);
        await Assert.That(settings.MatchesDocsBranch("main")).IsTrue();
        await Assert.That(settings.MatchesDocsBranch("master")).IsTrue();
        await Assert.That(settings.MatchesDocsBranch("develop")).IsFalse();
        await Assert.That(settings.MatchesDocsBranch(string.Empty)).IsFalse();
    }

    [Test]
    public async Task MatchesDocsBranch_UsesConfiguredPatterns()
    {
        var config = new BuildvanaConfig { Release = new() { GenerateDocsFrom = ["^release/.+$"] } };
        var settings = Parse([], config);
        await Assert.That(settings.MatchesDocsBranch("release/2.0")).IsTrue();
        await Assert.That(settings.MatchesDocsBranch("main")).IsFalse();
    }

    [Test]
    public async Task MatchesDocsBranch_Throws_OnInvalidPattern()
    {
        var config = new BuildvanaConfig { Release = new() { GenerateDocsFrom = ["["] } };
        var settings = Parse([], config);
        await Assert.That(() => settings.MatchesDocsBranch("main")).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_ReadsBumpEnum()
    {
        await Assert.That(Parse(["--bump", "minor"]).ResolveBump()).IsEqualTo(VersionSpecChange.Minor);
    }

    [Test]
    public async Task ResolveBump_Throws_OnInvalidValue()
    {
        var settings = Parse(["--bump", "bogus"]);
        await Assert.That(settings.ResolveBump).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_ReadsBoolOptions_SpaceAndInlineForms()
    {
        var settings = Parse(["--check-public-api", "false", "--dogfood=false"]);
        await Assert.That(settings.ResolveCheckPublicApi()).IsFalse();
        await Assert.That(settings.ResolveDogfood()).IsFalse();
    }

    [Test]
    public async Task Parse_Throws_OnInvalidBool()
    {
        await Assert.That(() => Parse(["--dogfood", "maybe"])).Throws<BuildFailedException>();
    }

    [Test]
    public async Task Parse_Throws_OnUnknownOption()
    {
        await Assert.That(() => Parse(["--bogus"])).Throws<BuildFailedException>();
    }

    private static ReleaseSettings Parse(string[] options, BuildvanaConfig? config = null)
    {
        config ??= new BuildvanaConfig();
        return ReleaseSettings.Parse(options, config, new DotNetSettings(config));
    }
}
