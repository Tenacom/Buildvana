// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services;

internal sealed class DotNetServicePushTargetTests
{
    [Test]
    public async Task ResolvePushTarget_Prerelease_UsesPrereleaseFeed()
    {
        const string envName = "BV_TEST_PRERELEASE_API_KEY";
        var feeds = Feeds(
            prerelease: new() { Source = "https://prerelease.example/v3/index.json", ApiKeyEnv = envName },
            release: new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED_API_KEY" });

        Environment.SetEnvironmentVariable(envName, "preview-key");
        try
        {
            var target = DotNetService.ResolvePushTarget(feeds, isPrerelease: true);
            await Assert.That(target.Source).IsEqualTo("https://prerelease.example/v3/index.json");
            await Assert.That(target.ApiKey).IsEqualTo("preview-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Test]
    public async Task ResolvePushTarget_Prerelease_FallsBackToReleaseFeed_WhenPrereleaseAbsent()
    {
        const string envName = "BV_TEST_RELEASE_FALLBACK_API_KEY";
        var feeds = Feeds(
            release: new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = envName });

        Environment.SetEnvironmentVariable(envName, "release-key");
        try
        {
            var target = DotNetService.ResolvePushTarget(feeds, isPrerelease: true);
            await Assert.That(target.Source).IsEqualTo("https://release.example/v3/index.json");
            await Assert.That(target.ApiKey).IsEqualTo("release-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Test]
    public async Task ResolvePushTarget_Stable_UsesReleaseFeed()
    {
        const string envName = "BV_TEST_RELEASE_API_KEY";
        var feeds = Feeds(
            prerelease: new() { Source = "https://prerelease.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED_API_KEY" },
            release: new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = envName });

        Environment.SetEnvironmentVariable(envName, "release-key");
        try
        {
            var target = DotNetService.ResolvePushTarget(feeds, isPrerelease: false);
            await Assert.That(target.Source).IsEqualTo("https://release.example/v3/index.json");
            await Assert.That(target.ApiKey).IsEqualTo("release-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenNoFeedConfigured()
    {
        var feeds = Feeds();
        await Assert.That(() => DotNetService.ResolvePushTarget(feeds, isPrerelease: false)).Throws<BuildFailedException>();
        await Assert.That(() => DotNetService.ResolvePushTarget(feeds, isPrerelease: true)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenPrereleaseFeedHasNoSource()
    {
        var feeds = Feeds(
            prerelease: new() { ApiKeyEnv = "BV_TEST_PRERELEASE_API_KEY" },
            release: new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED" });
        await Assert.That(() => DotNetService.ResolvePushTarget(feeds, isPrerelease: true)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenPrereleaseFeedHasNoApiKeyEnv()
    {
        var feeds = Feeds(
            prerelease: new() { Source = "https://prerelease.example/v3/index.json" },
            release: new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED" });
        await Assert.That(() => DotNetService.ResolvePushTarget(feeds, isPrerelease: true)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenReleaseFeedHasNoSource()
    {
        var feeds = Feeds(
            prerelease: new() { Source = "https://prerelease.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED" },
            release: new() { ApiKeyEnv = "BV_TEST_RELEASE_API_KEY" });
        await Assert.That(() => DotNetService.ResolvePushTarget(feeds, isPrerelease: false)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenReleaseFeedHasNoApiKeyEnv()
    {
        var feeds = Feeds(
            prerelease: new() { Source = "https://prerelease.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED" },
            release: new() { Source = "https://release.example/v3/index.json" });
        await Assert.That(() => DotNetService.ResolvePushTarget(feeds, isPrerelease: false)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenApiKeyEnvVarIsUnset()
    {
        const string envName = "BV_TEST_UNSET_API_KEY";
        Environment.SetEnvironmentVariable(envName, null);
        var feeds = Feeds(
            release: new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = envName });
        await Assert.That(() => DotNetService.ResolvePushTarget(feeds, isPrerelease: false)).Throws<BuildFailedException>();
    }

    private static NuGetFeedsConfig Feeds(NuGetFeedConfig? prerelease = null, NuGetFeedConfig? release = null)
        => new() { Prerelease = prerelease, Release = release };
}
