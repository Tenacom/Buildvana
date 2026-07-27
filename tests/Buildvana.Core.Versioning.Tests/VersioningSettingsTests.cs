// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.Versioning;

internal sealed class VersioningSettingsTests
{
    [Test]
    public async Task Constructor_EmptyConfig_AppliesDefaults()
    {
        var settings = new VersioningSettings(new BuildvanaConfig());
        await Assert.That(settings.PublicReleaseBranchPatterns.Count).IsEqualTo(0);
        await Assert.That(settings.PrereleaseTag).IsNull();
        await Assert.That(settings.AssemblyVersionPrecision).IsEqualTo(AssemblyVersionPrecision.Major);
    }

    [Test]
    public async Task Constructor_ReadsConfiguredValues()
    {
        var config = new BuildvanaConfig
        {
            Release = new ReleaseConfig { Branches = ["^main$"] },
            Versioning = new VersioningConfig
            {
                PrereleaseTag = "preview",
                AssemblyVersionPrecision = AssemblyVersionPrecision.Build,
            },
        };
        var settings = new VersioningSettings(config);
        await Assert.That(settings.PublicReleaseBranchPatterns.Count).IsEqualTo(1);
        await Assert.That(settings.PrereleaseTag).IsEqualTo("preview");
        await Assert.That(settings.AssemblyVersionPrecision).IsEqualTo(AssemblyVersionPrecision.Build);
    }

    [Test]
    [Arguments("main", true)]
    [Arguments("v2.0", true)]
    [Arguments("feature/x", false)]
    [Arguments("main2", false)]
    [Arguments("", false)]
    public async Task IsPublicReleaseBranch_MatchesConfiguredPatterns(string branch, bool expected)
    {
        var config = new BuildvanaConfig { Release = new ReleaseConfig { Branches = ["^main$", @"^v\d+\.\d+$"] } };
        var settings = new VersioningSettings(config);
        await Assert.That(settings.IsPublicReleaseBranch(branch)).IsEqualTo(expected);
    }

    [Test]
    public async Task IsPublicReleaseBranch_EmptyPatternList_NeverMatches()
    {
        var settings = new VersioningSettings(new BuildvanaConfig());
        await Assert.That(settings.IsPublicReleaseBranch("main")).IsFalse();
    }

    [Test]
    public async Task IsPublicReleaseBranch_InvalidPattern_ThrowsBuildFailedException()
    {
        var config = new BuildvanaConfig { Release = new ReleaseConfig { Branches = ["("] } };
        var settings = new VersioningSettings(config);
        BuildFailedException? exception = null;
        try
        {
            _ = settings.IsPublicReleaseBranch("main");
        }
        catch (BuildFailedException e)
        {
            exception = e;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("release.branches");
    }
}
