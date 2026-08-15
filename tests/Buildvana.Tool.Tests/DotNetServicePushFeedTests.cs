// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services;

// The environment does not come into play here: the API key is read through the feed's GetApiKey extension
// method (covered by its own tests), and a feed always carries source and apiKeyEnv, the schema requiring
// both. What is left to resolve is which channel a version pushes to.
internal sealed class DotNetServicePushFeedTests
{
    private static readonly NuGetFeedConfig PrereleaseFeed = new()
    {
        Source = "https://prerelease.example/v3/index.json",
        ApiKeyEnv = "BV_TEST_PRERELEASE_API_KEY",
    };

    private static readonly NuGetFeedConfig ReleaseFeed = new()
    {
        Source = "https://release.example/v3/index.json",
        ApiKeyEnv = "BV_TEST_RELEASE_API_KEY",
    };

    [Test]
    public async Task ResolvePushFeed_Prerelease_UsesPrereleaseFeed()
    {
        var feeds = new NuGetFeedsConfig { Prerelease = PrereleaseFeed, Release = ReleaseFeed };
        await Assert.That(DotNetService.ResolvePushFeed(feeds, isPrerelease: true)).IsSameReferenceAs(PrereleaseFeed);
    }

    [Test]
    public async Task ResolvePushFeed_Stable_UsesReleaseFeed()
    {
        var feeds = new NuGetFeedsConfig { Prerelease = PrereleaseFeed, Release = ReleaseFeed };
        await Assert.That(DotNetService.ResolvePushFeed(feeds, isPrerelease: false)).IsSameReferenceAs(ReleaseFeed);
    }

    // A null channel means no feed at all is configured for that kind of version: the configuration factory
    // fills the prerelease channel from the release feed when only the latter is stated.
    [Test]
    public async Task ResolvePushFeed_Throws_WhenNoFeedConfigured()
    {
        var feeds = new NuGetFeedsConfig();
        await Assert.That(() => DotNetService.ResolvePushFeed(feeds, isPrerelease: false)).Throws<BuildFailedException>();
        await Assert.That(() => DotNetService.ResolvePushFeed(feeds, isPrerelease: true)).Throws<BuildFailedException>();
    }
}
