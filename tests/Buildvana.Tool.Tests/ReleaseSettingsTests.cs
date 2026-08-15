// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.Versioning;
using Buildvana.Runtime;
using Buildvana.Tool.Services;
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

    // Hand-building a domain config would bypass the factory's release-to-dotnet configuration fallback,
    // so the configs come from the factory, the way production composes them.
    [Test]
    public async Task ResolveConfiguration_FollowsFlagOverReleaseOverDotNetChain()
    {
        var config = BuildvanaConfigFactory.Create(
            new BuildvanaJsonConfig
            {
                DotNet = new() { Configuration = "DotNetConfig" },
                Release = new() { Configuration = "ReleaseConfig" },
            },
            null);

        // Flag wins over both config sections.
        await Assert.That(Parse(["-c", "FlagConfig"], config).ResolveConfiguration()).IsEqualTo("FlagConfig");

        // With no flag, release.configuration wins over dotnet.configuration.
        await Assert.That(Parse([], config).ResolveConfiguration()).IsEqualTo("ReleaseConfig");

        // With neither flag nor release.configuration, dotnet.configuration is used.
        var dotNetOnly = BuildvanaConfigFactory.Create(
            new BuildvanaJsonConfig { DotNet = new() { Configuration = "DotNetConfig" } },
            null);
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

    // Substituting whitespace for an empty changelog would substitute nothing for nothing, so it counts
    // as no substitute at all and lets the release fail with the message that says so.
    [Test]
    public async Task ResolveEmptyChangelog_IsNull_WhenConfiguredTextIsAllWhitespace()
    {
        var config = new BuildvanaConfig { Release = new() { EmptyChangelog = " \n\t " } };
        await Assert.That(Parse([], config).ResolveEmptyChangelog()).IsNull();
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

    private static ReleaseSettings Parse(string[] options, BuildvanaConfig? config = null)
    {
        config ??= new BuildvanaConfig();
        return ReleaseSettings.Parse(options, config, new DotNetSettings(config));
    }
}
