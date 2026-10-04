// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.Services;
using NuGet.Versioning;

internal sealed class SelfUpdateTargetResolverTests
{
    private const string OwnVersion = "2.1.41-preview";

    private static BvManifestPin NoEntry => new(false, null, null);

    [Test]
    public async Task Resolve_WithTo_TakesTheVersionGiven_AndAsksNoSource()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.45-preview"]);
        var request = new SelfUpdateRequest { To = NuGetVersion.Parse("2.1.43-preview") };

        var target = await Resolve(source, request, Pin("2.1.41-preview")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.1.43-preview"));
        await Assert.That(target.Description).Contains("--to");
        await Assert.That(target.Description).Contains("2.1.43-preview");
        await Assert.That(source.Asked.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resolve_WithRepair_TakesTheManifestPin_AndAsksNoSource()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.45-preview"]);
        var request = new SelfUpdateRequest { Repair = true };

        var target = await Resolve(source, request, Pin("2.1.43-preview")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.1.43-preview"));
        await Assert.That(target.Description).Contains("tool manifest");
        await Assert.That(source.Asked.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resolve_WithRepair_AndNoManifestEntry_Fails()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.45-preview"]);
        var request = new SelfUpdateRequest { Repair = true };

        var exception = await Assert
            .That(async () => _ = await Resolve(source, request, NoEntry).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("--repair");
        await Assert.That(exception.Message).Contains("dotnet-tools.json");
        await Assert.That(source.Asked.Count).IsEqualTo(0);
    }

    // The default rule: the latest stable at or above the pin wins, however many prereleases sit above it.
    [Test]
    public async Task Resolve_ByDefault_WithPrereleasePin_TakesTheLatestStableAtOrAboveIt()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.10", "2.2.1-preview", "2.2.7", "2.3.1-preview"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, Pin("2.2.1-preview")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.2.7"));
        await Assert.That(target.Description).IsEqualTo("The latest bv on the package sources is 2.2.7");
        await Assert.That(source.Asked).IsEquivalentTo(["bv"]);
    }

    [Test]
    public async Task Resolve_ByDefault_WithPrereleasePin_AndNoStableAtOrAboveIt_TakesTheLatestPrerelease()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.10", "2.2.1-preview", "2.2.4-preview", "2.3.1-preview"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, Pin("2.2.1-preview")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.3.1-preview"));
    }

    [Test]
    public async Task Resolve_ByDefault_WithStablePin_TakesTheLatestStable()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.8", "2.1.10", "2.2.5-preview"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, Pin("2.1.8")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.1.10"));
    }

    // A stable pin never moves to a prerelease by itself: that takes --preview.
    [Test]
    public async Task Resolve_ByDefault_WithStablePin_AndOnlyPrereleasesAboveIt_StaysOnThePin()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.8", "2.2.5-preview"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, Pin("2.1.8")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.1.8"));
        await Assert.That(target.Description).IsEqualTo("The latest bv on the package sources is 2.1.8");
    }

    // A pin above everything the sources list is a repository ahead of its sources, not a downgrade: the
    // pin stays, and the description says why.
    [Test]
    public async Task Resolve_ByDefault_WithPinAboveEveryListedVersion_StaysOnThePin()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.10", "2.2.7"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, Pin("2.3.0-preview")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.3.0-preview"));
        await Assert.That(target.Description)
            .IsEqualTo("The tool manifest pins bv 2.3.0-preview, and the package sources list nothing newer");
    }

    [Test]
    public async Task Resolve_ByDefault_WithoutManifestEntry_StartsFromTheOwnVersion()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.40-preview", "2.1.45-preview"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, NoEntry).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.1.45-preview"));
    }

    [Test]
    public async Task Resolve_ByDefault_WithoutManifestEntry_AndNothingNewer_StaysOnTheOwnVersion()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.40-preview"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, NoEntry).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse(OwnVersion));
        await Assert.That(target.Description)
            .IsEqualTo("This bv is version 2.1.41-preview, and the package sources list nothing newer");
    }

    // A delisted version is often a vulnerable one: it is never a candidate.
    [Test]
    public async Task Resolve_ByDefault_IgnoresUnlistedVersions()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.8"], ["2.1.9"]);

        var target = await Resolve(source, SelfUpdateRequest.Default, Pin("2.1.8")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.1.8"));
    }

    [Test]
    public async Task Resolve_WithPreview_WithPrereleasePin_TakesTheLatestVersionOfAll()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.2.1-preview", "2.2.7", "2.3.1-preview"]);
        var request = new SelfUpdateRequest { Preview = true };

        var target = await Resolve(source, request, Pin("2.2.1-preview")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.3.1-preview"));
        await Assert.That(target.Description)
            .IsEqualTo("The latest bv on the package sources, prereleases included, is 2.3.1-preview");
    }

    [Test]
    public async Task Resolve_WithPreview_WithStablePin_MovesToAPrerelease()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.8", "2.2.5-preview"]);
        var request = new SelfUpdateRequest { Preview = true };

        var target = await Resolve(source, request, Pin("2.1.8")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.2.5-preview"));
    }

    [Test]
    public async Task Resolve_WithPreview_WithPinAboveEveryListedVersion_StaysOnThePin()
    {
        var source = new FakePackageVersionSource().Knows("bv", ["2.1.8", "2.2.5-preview"]);
        var request = new SelfUpdateRequest { Preview = true };

        var target = await Resolve(source, request, Pin("2.3.0-preview")).ConfigureAwait(false);

        await Assert.That(target.Version).IsEqualTo(NuGetVersion.Parse("2.3.0-preview"));
    }

    [Test]
    public async Task Resolve_WithoutSources_Fails()
    {
        var source = new FakePackageVersionSource { Sources = [] };

        var exception = await Assert
            .That(async () => _ = await Resolve(source, SelfUpdateRequest.Default, Pin("2.1.8")).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("No package source is configured");
    }

    // A source that knows nothing of bv is a misconfigured source, and the message names the sources so
    // that the user can tell which.
    [Test]
    public async Task Resolve_WhenNoSourceKnowsBv_Fails()
    {
        var source = new FakePackageVersionSource { Sources = ["nuget.org", "private"] };

        var exception = await Assert
            .That(async () => _ = await Resolve(source, SelfUpdateRequest.Default, Pin("2.1.8")).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("No configured package source knows bv");
        await Assert.That(exception.Message).Contains("nuget.org, private");
    }

    private static BvManifestPin Pin(string version) => new(true, version, NuGetVersion.Parse(version));

    private static Task<SelfUpdateTarget> Resolve(FakePackageVersionSource source, SelfUpdateRequest request, BvManifestPin pin)
        => new SelfUpdateTargetResolver(source, NuGetVersion.Parse(OwnVersion)).ResolveAsync(request, pin);
}
