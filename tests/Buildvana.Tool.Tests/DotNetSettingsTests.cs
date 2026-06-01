// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Tool.Services;

internal sealed class DotNetSettingsTests
{
    [Test]
    public async Task ResolvePushTarget_Prerelease_UsesPrereleaseFeed()
    {
        const string envName = "BV_TEST_PRERELEASE_API_KEY";
        var config = ConfigWithFeeds(
            ("prerelease", new() { Source = "https://prerelease.example/v3/index.json", ApiKeyEnv = envName }),
            ("release", new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED_API_KEY" }));

        Environment.SetEnvironmentVariable(envName, "preview-key");
        try
        {
            var target = new DotNetSettings(config).ResolvePushTarget(isPrerelease: true);
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
        var config = ConfigWithFeeds(
            ("release", new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = envName }));

        Environment.SetEnvironmentVariable(envName, "release-key");
        try
        {
            var target = new DotNetSettings(config).ResolvePushTarget(isPrerelease: true);
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
        var config = ConfigWithFeeds(
            ("prerelease", new() { Source = "https://prerelease.example/v3/index.json", ApiKeyEnv = "BV_TEST_UNUSED_API_KEY" }),
            ("release", new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = envName }));

        Environment.SetEnvironmentVariable(envName, "release-key");
        try
        {
            var target = new DotNetSettings(config).ResolvePushTarget(isPrerelease: false);
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
        var settings = new DotNetSettings(new BuildvanaConfig());
        await Assert.That(() => settings.ResolvePushTarget(isPrerelease: false)).Throws<BuildFailedException>();
        await Assert.That(() => settings.ResolvePushTarget(isPrerelease: true)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenFeedHasNoSource()
    {
        var config = ConfigWithFeeds(("release", new() { ApiKeyEnv = "BV_TEST_RELEASE_API_KEY" }));
        var settings = new DotNetSettings(config);
        await Assert.That(() => settings.ResolvePushTarget(isPrerelease: false)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenFeedHasNoApiKeyEnv()
    {
        var config = ConfigWithFeeds(("release", new() { Source = "https://release.example/v3/index.json" }));
        var settings = new DotNetSettings(config);
        await Assert.That(() => settings.ResolvePushTarget(isPrerelease: false)).Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolvePushTarget_Throws_WhenApiKeyEnvVarIsUnset()
    {
        const string envName = "BV_TEST_UNSET_API_KEY";
        Environment.SetEnvironmentVariable(envName, null);
        var config = ConfigWithFeeds(
            ("release", new() { Source = "https://release.example/v3/index.json", ApiKeyEnv = envName }));
        var settings = new DotNetSettings(config);
        await Assert.That(() => settings.ResolvePushTarget(isPrerelease: false)).Throws<BuildFailedException>();
    }

    private static BuildvanaConfig ConfigWithFeeds(params (string Channel, NuGetFeedConfig Feed)[] feeds)
    {
        var dictionary = new Dictionary<string, NuGetFeedConfig>(StringComparer.Ordinal);
        foreach (var (channel, feed) in feeds)
        {
            dictionary[channel] = feed;
        }

        return new BuildvanaConfig { NuGet = new() { Feeds = dictionary } };
    }
}
