// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Runtime;

internal sealed class BuildvanaConfigFactoryTests
{
    // A run with no configuration file and no command line resolves to the domain defaults, member for
    // member: the factory starts from them and had nothing to overlay.
    [Test]
    public async Task Create_NoSources_YieldsDomainDefaults()
    {
        var config = BuildvanaConfigFactory.Create(null, null);

        await Assert.That(config.Release.Branches.Count).IsEqualTo(0);
        await Assert.That(config.Release.Configuration).IsEqualTo("Release");
        await Assert.That(config.Release.CheckPublicApi).IsTrue();
        await Assert.That(config.Release.ChangelogUpdates).IsEqualTo(ChangelogUpdates.Stable);
        await Assert.That(config.Release.EmptyChangelog).IsNull();
        await Assert.That(config.Release.Dogfood).IsTrue();
        await Assert.That(config.Versioning.PrereleaseTag).IsNull();
        await Assert.That(config.Versioning.AssemblyVersionPrecision).IsEqualTo(AssemblyVersionPrecision.Major);
        await Assert.That(config.DotNet.Configuration).IsEqualTo("Release");
        await Assert.That(config.DotNet.All.Args.Count).IsEqualTo(0);
        await Assert.That(config.DotNet.All.Env.Count).IsEqualTo(0);
        await Assert.That(config.NuGet.Feeds.Prerelease).IsNull();
        await Assert.That(config.NuGet.Feeds.Release).IsNull();
        await Assert.That(config.GitHub.TokenEnv).IsEqualTo("GITHUB_TOKEN");
        await Assert.That(config.Git.Identity).IsNull();
    }

    [Test]
    public async Task Create_FileValues_OverrideDefaults()
    {
        var json = new BuildvanaJsonConfig
        {
            Release = new()
            {
                Branches = ["^main$"],
                CheckPublicApi = false,
                ChangelogUpdates = ChangelogUpdates.All,
                EmptyChangelog = "No changes.",
                Dogfood = false,
            },
            Versioning = new()
            {
                PrereleaseTag = "preview",
                AssemblyVersionPrecision = AssemblyVersionPrecision.Build,
            },
            DotNet = new() { Configuration = "Debug" },
            GitHub = new() { TokenEnv = "MY_TOKEN" },
        };

        var config = BuildvanaConfigFactory.Create(json, null);

        await Assert.That(config.Release.Branches.Count).IsEqualTo(1);
        await Assert.That(config.Release.Branches[0]).IsEqualTo("^main$");
        await Assert.That(config.Release.CheckPublicApi).IsFalse();
        await Assert.That(config.Release.ChangelogUpdates).IsEqualTo(ChangelogUpdates.All);
        await Assert.That(config.Release.EmptyChangelog).IsEqualTo("No changes.");
        await Assert.That(config.Release.Dogfood).IsFalse();
        await Assert.That(config.Versioning.PrereleaseTag).IsEqualTo("preview");
        await Assert.That(config.Versioning.AssemblyVersionPrecision).IsEqualTo(AssemblyVersionPrecision.Build);
        await Assert.That(config.DotNet.Configuration).IsEqualTo("Debug");
        await Assert.That(config.GitHub.TokenEnv).IsEqualTo("MY_TOKEN");
    }

    [Test]
    public async Task Create_CommandLine_BeatsFileAndDefaults()
    {
        var json = new BuildvanaJsonConfig
        {
            Release = new() { CheckPublicApi = true, Dogfood = true },
            DotNet = new() { Configuration = "Debug" },
        };
        var commandLine = new CommandLineOverrides
        {
            Configuration = "Custom",
            CheckPublicApi = false,
            Dogfood = false,
        };

        var config = BuildvanaConfigFactory.Create(json, commandLine);

        await Assert.That(config.DotNet.Configuration).IsEqualTo("Custom");
        await Assert.That(config.Release.Configuration).IsEqualTo("Custom");
        await Assert.That(config.Release.CheckPublicApi).IsFalse();
        await Assert.That(config.Release.Dogfood).IsFalse();
    }

    [Test]
    public async Task Create_ReleaseConfiguration_FollowsFileOverDotNetChain()
    {
        // With no release-specific configuration, the release follows dotnet.configuration.
        var dotNetOnly = BuildvanaConfigFactory.Create(
            new BuildvanaJsonConfig { DotNet = new() { Configuration = "Debug" } },
            null);
        await Assert.That(dotNetOnly.Release.Configuration).IsEqualTo("Debug");

        // A release-specific configuration wins over dotnet.configuration.
        var both = BuildvanaConfigFactory.Create(
            new BuildvanaJsonConfig
            {
                Release = new() { Configuration = "ReleaseOnly" },
                DotNet = new() { Configuration = "Debug" },
            },
            null);
        await Assert.That(both.Release.Configuration).IsEqualTo("ReleaseOnly");
        await Assert.That(both.DotNet.Configuration).IsEqualTo("Debug");
    }

    // Text that is all whitespace would substitute nothing for nothing, so the factory folds it to null.
    [Test]
    public async Task Create_EmptyChangelog_BlankNormalizesToNull()
    {
        var json = new BuildvanaJsonConfig { Release = new() { EmptyChangelog = " \n\t " } };
        await Assert.That(BuildvanaConfigFactory.Create(json, null).Release.EmptyChangelog).IsNull();
    }

    // A blank tokenEnv cannot name a variable, so the factory treats it as not stated.
    [Test]
    public async Task Create_TokenEnv_BlankFallsBackToDefault()
    {
        var json = new BuildvanaJsonConfig { GitHub = new() { TokenEnv = string.Empty } };
        await Assert.That(BuildvanaConfigFactory.Create(json, null).GitHub.TokenEnv).IsEqualTo("GITHUB_TOKEN");
    }

    [Test]
    public async Task Create_DotNetInvocations_MapPerCommand()
    {
        var json = new BuildvanaJsonConfig
        {
            DotNet = new()
            {
                All = new() { Args = ["-common"], Env = new Dictionary<string, string?> { ["A"] = "1" } },
                Test = new() { Args = ["--coverage"], Env = new Dictionary<string, string?> { ["B"] = null } },
            },
        };

        var config = BuildvanaConfigFactory.Create(json, null);

        await Assert.That(config.DotNet.All.Args[0]).IsEqualTo("-common");
        await Assert.That(config.DotNet.All.Env["A"]).IsEqualTo("1");
        await Assert.That(config.DotNet.Test.Args[0]).IsEqualTo("--coverage");
        await Assert.That(config.DotNet.Test.Env["B"]).IsNull();
        await Assert.That(config.DotNet.Pack.Args.Count).IsEqualTo(0);
        await Assert.That(config.DotNet.Pack.Env.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Create_Feeds_MapVerbatim()
    {
        var json = new BuildvanaJsonConfig
        {
            NuGet = new()
            {
                Feeds = new()
                {
                    Release = new() { Source = "https://nuget.example/v3/index.json", ApiKeyEnv = "KEY" },
                },
            },
        };

        var config = BuildvanaConfigFactory.Create(json, null);

        await Assert.That(config.NuGet.Feeds.Release!.Source).IsEqualTo("https://nuget.example/v3/index.json");
        await Assert.That(config.NuGet.Feeds.Release.ApiKeyEnv).IsEqualTo("KEY");
        await Assert.That(config.NuGet.Feeds.Prerelease).IsNull();
    }

    [Test]
    public async Task Create_GitIdentity_MapsNameAndEmail()
    {
        var json = new BuildvanaJsonConfig
        {
            Git = new() { Identity = new() { Name = "Buildvana Bot", Email = "bot@buildvana.invalid" } },
        };

        var config = BuildvanaConfigFactory.Create(json, null);

        await Assert.That(config.Git.Identity!.Name).IsEqualTo("Buildvana Bot");
        await Assert.That(config.Git.Identity.Email).IsEqualTo("bot@buildvana.invalid");
    }
}
