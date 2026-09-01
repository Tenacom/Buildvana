// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

// The index and its per-channel files are canned here: what the reader is judged on is what it makes of the
// shape Microsoft publishes, not whether the network is up.
internal sealed class DotNetReleaseIndexTests
{
    private const string ChannelUrl11 = "https://example.test/11.0/releases.json";
    private const string ChannelUrl10 = "https://example.test/10.0/releases.json";
    private const string ChannelUrl9 = "https://example.test/9.0/releases.json";

    private const string Index = """
                                 {
                                   "releases-index": [
                                     {
                                       "channel-version": "11.0",
                                       "release-type": "sts",
                                       "releases.json": "https://example.test/11.0/releases.json"
                                     },
                                     {
                                       "channel-version": "10.0",
                                       "release-type": "lts",
                                       "releases.json": "https://example.test/10.0/releases.json"
                                     },
                                     {
                                       "channel-version": "9.0",
                                       "release-type": "sts",
                                       "releases.json": "https://example.test/9.0/releases.json"
                                     }
                                   ]
                                 }
                                 """;

    private const string Channel11 = """
                                     {
                                       "channel-version": "11.0",
                                       "releases": [
                                         {
                                           "release-version": "11.0.0-preview.1",
                                           "sdk": { "version": "11.0.100-preview.1" },
                                           "sdks": [ { "version": "11.0.100-preview.1" } ]
                                         }
                                       ]
                                     }
                                     """;

    private const string Channel10 = """
                                     {
                                       "channel-version": "10.0",
                                       "releases": [
                                         {
                                           "release-version": "10.0.1",
                                           "sdk": { "version": "10.0.101" },
                                           "sdks": [ { "version": "10.0.101" }, { "version": "10.0.201" }, { "version": "ten" } ]
                                         },
                                         {
                                           "release-version": "10.0.0",
                                           "sdk": { "version": "10.0.100" },
                                           "sdks": [ { "version": "10.0.100" } ]
                                         }
                                       ]
                                     }
                                     """;

    private const string Channel9 = """
                                    {
                                      "channel-version": "9.0",
                                      "releases": [
                                        {
                                          "release-version": "9.0.0",
                                          "sdk": { "version": "9.0.100" },
                                          "sdks": [ { "version": "9.0.100" } ]
                                        }
                                      ]
                                    }
                                    """;

    [Test]
    public async Task GetReleasesAsync_AsksTheOfficialIndexFirst()
    {
        var handler = NewHandler();
        using var index = new DotNetReleaseIndex(handler);
        _ = await index.GetReleasesAsync(NuGetVersion.Parse("10.0.100")).ConfigureAwait(false);
        await Assert.That(handler.Requests[0]).IsEqualTo(DotNetReleaseIndex.IndexUrl);
    }

    [Test]
    public async Task GetReleasesAsync_ReadsTheChannelOfThePinAndEveryNewerOne()
    {
        var handler = NewHandler();
        using var index = new DotNetReleaseIndex(handler);
        _ = await index.GetReleasesAsync(NuGetVersion.Parse("10.0.100")).ConfigureAwait(false);
        await Assert.That(handler.Requests).Contains(ChannelUrl10);
        await Assert.That(handler.Requests).Contains(ChannelUrl11);
        await Assert.That(handler.Requests).DoesNotContain(ChannelUrl9);
    }

    [Test]
    public async Task GetReleasesAsync_StatesEverySdkOfAReleaseOnce()
    {
        using var index = new DotNetReleaseIndex(NewHandler());
        var releases = await index.GetReleasesAsync(NuGetVersion.Parse("10.0.100")).ConfigureAwait(false);
        await Assert.That(releases.Select(static release => release.Version.ToNormalizedString()))
            .IsEquivalentTo(["11.0.100-preview.1", "10.0.101", "10.0.201", "10.0.100"]);
    }

    [Test]
    public async Task GetReleasesAsync_MarksTheReleasesOfALongTermSupportChannel()
    {
        using var index = new DotNetReleaseIndex(NewHandler());
        var releases = await index.GetReleasesAsync(NuGetVersion.Parse("10.0.100")).ConfigureAwait(false);
        await Assert.That(releases.Where(static release => release.IsLts).Select(static release => release.Version.Major))
            .IsEquivalentTo([10, 10, 10]);
        await Assert.That(releases.Where(static release => !release.IsLts).Select(static release => release.Version.Major))
            .IsEquivalentTo([11]);
    }

    [Test]
    public async Task GetReleasesAsync_WhenAChannelCannotBeRead_Fails()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string> { [DotNetReleaseIndex.IndexUrl] = Index });
        using var index = new DotNetReleaseIndex(handler);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await index.GetReleasesAsync(NuGetVersion.Parse("10.0.100")).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task GetReleasesAsync_WhenTheIndexIsNotJson_Fails()
    {
        var handler = new StubHttpMessageHandler(new Dictionary<string, string> { [DotNetReleaseIndex.IndexUrl] = "not json" });
        using var index = new DotNetReleaseIndex(handler);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await index.GetReleasesAsync(NuGetVersion.Parse("10.0.100")).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    private static StubHttpMessageHandler NewHandler()
        => new(new Dictionary<string, string>
        {
            [DotNetReleaseIndex.IndexUrl] = Index,
            [ChannelUrl11] = Channel11,
            [ChannelUrl10] = Channel10,
            [ChannelUrl9] = Channel9,
        });
}
