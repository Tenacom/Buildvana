// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class DependencyResolverTests
{
    [Test]
    public async Task ResolveAsync_MovesAPinAsFarAsItsPolicyAllows()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.0.0", "3.1.0", "4.0.0"]);
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0"))).ConfigureAwait(false);
        var pin = resolution.Packages.Single();
        await Assert.That(pin.State).IsEqualTo(PinResolutionState.Updated);
        await Assert.That(pin.Target?.ToNormalizedString()).IsEqualTo("3.1.0");
        await Assert.That(pin.LatestStable?.ToNormalizedString()).IsEqualTo("4.0.0");
        await Assert.That(pin.LatestPreview).IsNull();
    }

    [Test]
    public async Task ResolveAsync_APinAtItsTarget_IsUpToDate()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.0.0", "3.1.0"]);
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "3.1.0"))).ConfigureAwait(false);
        await Assert.That(resolution.Packages.Single().State).IsEqualTo(PinResolutionState.UpToDate);
    }

    [Test]
    public async Task ResolveAsync_AnUnmanagedPin_IsNotLookedUp()
    {
        var versions = new FakePackageVersionSource();
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "[3.0.0,4.0.0)"))).ConfigureAwait(false);
        await Assert.That(resolution.Packages.Single().State).IsEqualTo(PinResolutionState.Unmanaged);
        await Assert.That(resolution.Packages.Single().Note).IsNotEmpty();
        await Assert.That(versions.Asked).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_APinItsPolicyDisables_IsNotLookedUp()
    {
        var config = new DependenciesConfig { Scopes = new DependencyScopesConfig { Packages = "disable" } };
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.0.0", "4.0.0"]);
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0")), config).ConfigureAwait(false);
        await Assert.That(resolution.Packages.Single().State).IsEqualTo(PinResolutionState.Disabled);
        await Assert.That(versions.Asked).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_ADelistedPin_MovesAndSaysSo()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.1.0"], unlisted: ["3.0.0"]);
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0"))).ConfigureAwait(false);
        var pin = resolution.Packages.Single();
        await Assert.That(pin.State).IsEqualTo(PinResolutionState.Updated);
        await Assert.That(pin.Note).IsEqualTo(PinNotes.Delisted);
    }

    [Test]
    public async Task ResolveAsync_APrereleasePinUnderAStablePolicy_IsHeldAndSaysSo()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.0.0-beta.1"]);
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0-beta.1"))).ConfigureAwait(false);
        var pin = resolution.Packages.Single();
        await Assert.That(pin.State).IsEqualTo(PinResolutionState.Held);
        await Assert.That(pin.Note).IsNotEmpty();
    }

    [Test]
    public async Task ResolveAsync_AnIdNoSourceKnows_Fails()
    {
        var versions = new FakePackageVersionSource();
        var exception = await Assert.That(async () => await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0"))).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        var diagnostic = exception!.Diagnostics.Single();
        await Assert.That(diagnostic.Code).IsEqualTo("BV1200");
        await Assert.That(diagnostic.File).IsEqualTo("Directory.Packages.props");
    }

    [Test]
    public async Task ResolveAsync_AVersionNoSourceHas_Fails()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.1.0"]);
        var exception = await Assert.That(async () => await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0"))).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Diagnostics.Single().Code).IsEqualTo("BV1201");
    }

    [Test]
    public async Task ResolveAsync_ReportsEveryPinItCannotResolve()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.1.0"]);
        var inventory = Packages(Pin("Serilog", "3.0.0"), Pin("Newtonsoft.Json", "13.0.3"));
        var exception = await Assert.That(async () => await ResolveAsync(versions, inventory).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Diagnostics.Select(static diagnostic => diagnostic.Code)).IsEquivalentTo(["BV1201", "BV1200"]);
    }

    [Test]
    public async Task ResolveAsync_WithNoConfiguredSource_Fails()
    {
        var versions = new FakePackageVersionSource { Sources = [] };
        await Assert.That(async () => await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0"))).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task ResolveAsync_MovesTheNetSdkWithinItsPolicy()
    {
        var releases = new FakeNetSdkReleaseSource().Knows(isLts: true, "10.0.100", "10.0.101", "10.0.201");
        var resolution = await ResolveNetSdkAsync(releases, NetSdkPin.Create("10.0.100", allowPrerelease: false)).ConfigureAwait(false);
        await Assert.That(resolution.NetSdk!.State).IsEqualTo(PinResolutionState.Updated);
        await Assert.That(resolution.NetSdk.Target?.ToNormalizedString()).IsEqualTo("10.0.201");
        await Assert.That(resolution.NetSdk.WritesAllowPrerelease).IsFalse();
    }

    [Test]
    public async Task ResolveAsync_WhenAllowPrereleaseDisagreesWithThePolicy_SaysItMustBeWritten()
    {
        var releases = new FakeNetSdkReleaseSource().Knows(isLts: true, "10.0.100");
        var resolution = await ResolveNetSdkAsync(releases, NetSdkPin.Create("10.0.100", allowPrerelease: null)).ConfigureAwait(false);
        await Assert.That(resolution.NetSdk!.WritesAllowPrerelease).IsTrue();
        await Assert.That(resolution.NetSdk.Note).IsNotEmpty();
    }

    [Test]
    public async Task ResolveAsync_ANetSdkVersionTheIndexHasNot_Fails()
    {
        var releases = new FakeNetSdkReleaseSource().Knows(isLts: true, "10.0.100");
        var exception = await Assert
            .That(async () => await ResolveNetSdkAsync(releases, NetSdkPin.Create("10.0.999", allowPrerelease: false)).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        var diagnostic = exception!.Diagnostics.Single();
        await Assert.That(diagnostic.Code).IsEqualTo("BV1202");
        await Assert.That(diagnostic.File).IsEqualTo("global.json");
    }

    [Test]
    public async Task ResolveAsync_SkipsAPinNoFilterNames()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.0.0", "3.1.0"]).Knows("Newtonsoft.Json", ["13.0.3"]);
        var inventory = Packages(Pin("Serilog", "3.0.0"), Pin("Newtonsoft.Json", "13.0.3"));
        var request = new DependencyResolutionRequest { Filters = ["Newtonsoft.*"] };
        var resolution = await ResolveAsync(versions, inventory, request: request).ConfigureAwait(false);
        await Assert.That(resolution.Packages[0].State).IsEqualTo(PinResolutionState.Skipped);
        await Assert.That(resolution.Packages[1].State).IsEqualTo(PinResolutionState.UpToDate);
        await Assert.That(versions.Asked).IsEquivalentTo(["Newtonsoft.Json"]);
    }

    [Test]
    public async Task ResolveAsync_WithAFilterMatchingNoPin_ResolvesNothingAndFailsNot()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.0.0", "3.1.0"]);
        var request = new DependencyResolutionRequest { Filters = ["Northwind.*"] };
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0")), request: request).ConfigureAwait(false);
        await Assert.That(resolution.Packages.Single().State).IsEqualTo(PinResolutionState.Skipped);
        await Assert.That(resolution.HasPendingWork).IsFalse();
    }

    // A stated version is the user's own edit: it overrules the policy, and it may lower a pin.
    [Test]
    public async Task ResolveAsync_WithAStatedVersion_TakesItWhateverThePolicySays()
    {
        var config = new DependenciesConfig { Scopes = new DependencyScopesConfig { Packages = "disable" } };
        var versions = new FakePackageVersionSource().Knows("Serilog", ["2.0.0", "3.0.0"]);
        var request = new DependencyResolutionRequest { Filters = ["Serilog"], To = NuGetVersion.Parse("2.0.0") };
        var resolution = await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0")), config, request).ConfigureAwait(false);
        var pin = resolution.Packages.Single();
        await Assert.That(pin.State).IsEqualTo(PinResolutionState.Updated);
        await Assert.That(pin.Target?.ToNormalizedString()).IsEqualTo("2.0.0");
    }

    [Test]
    public async Task ResolveAsync_WithAStatedVersionNoSourceHas_Fails()
    {
        var versions = new FakePackageVersionSource().Knows("Serilog", ["3.0.0"]);
        var request = new DependencyResolutionRequest { Filters = ["Serilog"], To = NuGetVersion.Parse("9.9.9") };
        var exception = await Assert
            .That(async () => await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0")), request: request).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Diagnostics.Select(static diagnostic => diagnostic.Code)).Contains("BV1201");
    }

    [Test]
    public async Task ResolveAsync_WithAStatedVersionForAnIdNoPinHas_Fails()
    {
        var versions = new FakePackageVersionSource().Knows("Northwind.Data", ["1.0.0"]);
        var request = new DependencyResolutionRequest { Filters = ["Northwind.Data"], To = NuGetVersion.Parse("1.0.0") };
        var exception = await Assert
            .That(async () => await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0")), request: request).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Diagnostics.Single().Code).IsEqualTo("BV1203");
    }

    // A family id has no pin bv manages, and the message says where to go instead.
    [Test]
    public async Task ResolveAsync_WithAStatedVersionForAFamilyId_PointsAtSelfUpdate()
    {
        var versions = new FakePackageVersionSource().Knows("Buildvana.Sdk", ["2.1.0"]);
        var request = new DependencyResolutionRequest { Filters = ["Buildvana.Sdk"], To = NuGetVersion.Parse("2.1.0") };
        var exception = await Assert
            .That(async () => await ResolveAsync(versions, Packages(Pin("Serilog", "3.0.0")), request: request).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Diagnostics.Single().Message).Contains("bv self-update");
    }

    [Test]
    public async Task ResolveAsync_WithAStatedVersionForTheNetSdk_TakesIt()
    {
        var releases = new FakeNetSdkReleaseSource().Knows(isLts: true, "10.0.100", "10.0.201");
        var request = new DependencyResolutionRequest { To = NuGetVersion.Parse("10.0.100") };
        var pin = NetSdkPin.Create("10.0.201", allowPrerelease: false);
        var resolution = await ResolveNetSdkAsync(releases, pin, request).ConfigureAwait(false);
        await Assert.That(resolution.NetSdk!.State).IsEqualTo(PinResolutionState.Updated);
        await Assert.That(resolution.NetSdk.Target?.ToNormalizedString()).IsEqualTo("10.0.100");
    }

    [Test]
    public async Task ResolveAsync_WithAStatedNetSdkVersionTheIndexHasNot_Fails()
    {
        var releases = new FakeNetSdkReleaseSource().Knows(isLts: true, "10.0.100");
        var request = new DependencyResolutionRequest { To = NuGetVersion.Parse("10.0.999") };
        var pin = NetSdkPin.Create("10.0.100", allowPrerelease: false);
        var exception = await Assert.That(async () => await ResolveNetSdkAsync(releases, pin, request).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Diagnostics.Single().Code).IsEqualTo("BV1202");
    }

    private static DependencyPin Pin(string id, string versionText)
        => DependencyPin.Create(DependencyScope.Packages, id, versionText, "Directory.Packages.props");

    private static DependencyInventory Packages(params DependencyPin[] pins) => new() { Packages = pins };

    private static Task<DependencyResolution> ResolveAsync(
        FakePackageVersionSource versions,
        DependencyInventory inventory,
        DependenciesConfig? config = null,
        DependencyResolutionRequest? request = null)
    {
        var resolver = new DependencyResolver(
            versions,
            new FakeNetSdkReleaseSource(),
            new EffectivePolicyResolver(config ?? new DependenciesConfig()));
        return resolver.ResolveAsync(inventory, request ?? DependencyResolutionRequest.None);
    }

    private static Task<DependencyResolution> ResolveNetSdkAsync(
        FakeNetSdkReleaseSource releases,
        NetSdkPin pin,
        DependencyResolutionRequest? request = null)
    {
        var resolver = new DependencyResolver(
            new FakePackageVersionSource(),
            releases,
            new EffectivePolicyResolver(new DependenciesConfig()));
        return resolver.ResolveAsync(new DependencyInventory { NetSdk = pin }, request ?? DependencyResolutionRequest.None);
    }
}
