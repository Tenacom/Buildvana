// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;
using Spectre.Console.Testing;

internal sealed class DependencyReportRendererTests
{
    private static readonly DependencyScope[] AllScopes =
        [DependencyScope.NetSdk, DependencyScope.Sdks, DependencyScope.Tools, DependencyScope.Packages];

    [Test]
    public async Task Write_StatesEveryPinWithItsPolicy()
    {
        var inventory = new DependencyInventory
        {
            NetSdk = NetSdkPin.Create("10.0.100", allowPrerelease: false),
            Tools = [DependencyPin.Create(DependencyScope.Tools, "ngbv", "0.5.1", ".config/dotnet-tools.json")],
            Packages = [DependencyPin.Create(DependencyScope.Packages, "Serilog", "4.0.0", "Directory.Packages.props")],
        };

        var output = Render(inventory, AllScopes);
        await Assert.That(output).Contains(".NET SDK");
        await Assert.That(output).Contains("(the .NET SDK) 10.0.100 (major)"); // the netsdk scope's default policy
        await Assert.That(output).Contains(".config/dotnet-tools.json");
        await Assert.That(output).Contains("ngbv");
        await Assert.That(output).Contains("Directory.Packages.props");
        await Assert.That(output).Contains("Serilog 4.0.0 (minor)"); // the packages scope's default policy
    }

    // A scope nobody selected is not a scope with nothing in it, and the report says neither about it.
    [Test]
    public async Task Write_SaysNothingAboutAnUnselectedScope()
    {
        var inventory = new DependencyInventory { NetSdk = NetSdkPin.Create("10.0.100", allowPrerelease: false) };
        var output = Render(inventory, [DependencyScope.NetSdk]);
        await Assert.That(output).Contains(".NET SDK");
        await Assert.That(output).DoesNotContain("local tools");
        await Assert.That(output).DoesNotContain("NuGet packages");
    }

    [Test]
    public async Task Write_OfASelectedScopeWithNoPin_SaysSo()
    {
        var output = Render(new DependencyInventory(), [DependencyScope.Tools]);
        await Assert.That(output).Contains("nothing pinned");
    }

    // The packages scope has group sections of its own, and says it too when it has neither a group nor a
    // pin outside one.
    [Test]
    public async Task Write_OfThePackagesScopeWithNoPin_SaysSo()
    {
        var output = Render(new DependencyInventory(), [DependencyScope.Packages]);
        await Assert.That(output).Contains("nothing pinned");
    }

    [Test]
    public async Task Write_WithNoNetSdkPin_SaysSo()
    {
        var output = Render(new DependencyInventory(), [DependencyScope.NetSdk]);
        await Assert.That(output).Contains("pins no .NET SDK version");
    }

    [Test]
    [Arguments("[13.0.4]", "one version in brackets")]
    [Arguments("[1.0,2.0)", "a version range")]
    [Arguments("1.*", "a floating version")]
    public async Task Write_OfAnUnmanagedPin_SaysWhy(string versionText, string expected)
    {
        var pin = DependencyPin.Create(DependencyScope.Packages, "Newtonsoft.Json", versionText, "Directory.Packages.props");
        var output = Render(new DependencyInventory { Packages = [pin] }, [DependencyScope.Packages]);
        await Assert.That(output).Contains("not managed");
        await Assert.That(output).Contains(expected);
    }

    // Nothing moves such a pin, and nothing moves it back either: the reader is the one who decides.
    [Test]
    public async Task Write_OfAPrereleaseUnderAStableOnlyPolicy_SaysSo()
    {
        var pin = DependencyPin.Create(DependencyScope.Packages, "Serilog", "5.0.0-preview.1", "Directory.Packages.props");
        var output = Render(new DependencyInventory { Packages = [pin] }, [DependencyScope.Packages]);
        await Assert.That(output).Contains("a prerelease under a policy that takes only stable versions");
    }

    [Test]
    public async Task Write_WhenAllowPrereleaseDisagreesWithThePolicy_SaysSo()
    {
        var inventory = new DependencyInventory { NetSdk = NetSdkPin.Create("10.0.100", allowPrerelease: true) };
        var output = Render(inventory, [DependencyScope.NetSdk]);
        await Assert.That(output).Contains("allowPrerelease");
    }

    [Test]
    public async Task Write_StatesAGroupsPinsUnderItsCaption()
    {
        var pin = DependencyPin.Create(DependencyScope.Packages, "StyleCop.Analyzers", "1.2.0", "src/Sdk/PackageVersions.props")
            with { GroupCaption = "SDK package injections" };
        var output = Render(new DependencyInventory { Packages = [pin] }, [DependencyScope.Packages]);
        await Assert.That(output).Contains("NuGet packages: SDK package injections");
        await Assert.That(output).Contains("StyleCop.Analyzers");

        // The scope has pins, so the heading above the group's section says nothing rather than the contrary.
        await Assert.That(output).DoesNotContain("nothing pinned");
    }

    [Test]
    public async Task WriteOverrides_StatesEachEntryUnderTheFileThatHoldsIt()
    {
        var output = RenderOverrides(
        [
            new TransitiveOverrideEntry("Newtonsoft.Json", "13.0.3", "Directory.TransitiveOverrides.props"),
            new TransitiveOverrideEntry("Serilog", null, "src/Test/Test.TransitiveOverrides.props"),
        ]);

        await Assert.That(output).Contains("Transitive overrides");
        await Assert.That(output).Contains("Directory.TransitiveOverrides.props");
        await Assert.That(output).Contains("Newtonsoft.Json 13.0.3");
        await Assert.That(output).Contains("src/Test/Test.TransitiveOverrides.props");
        await Assert.That(output).Contains("Serilog at the version the repository pins");
    }

    [Test]
    public async Task WriteOverrides_WithNone_SaysSo()
        => await Assert.That(RenderOverrides([])).Contains("none in effect");

    [Test]
    public async Task WritePrune_StatesEveryOrphanUnderItsFile()
    {
        var orphans = new[]
        {
            DependencyPin.Create(DependencyScope.Packages, "Serilog", "3.0.0", "Directory.Packages.props"),
            DependencyPin.Create(DependencyScope.Packages, "Newtonsoft.Json", "13.0.3", "Directory.Packages.props"),
        };

        var output = RenderPrune(orphans, removed: true);
        await Assert.That(output).Contains("Orphaned NuGet package pins");
        await Assert.That(output).Contains("Directory.Packages.props");
        await Assert.That(output).Contains("Serilog 3.0.0 -> removed");
        await Assert.That(output).Contains("Newtonsoft.Json 13.0.3 -> removed");
        await Assert.That(output).Contains("2 pins removed.");
    }

    // A check run states what it would do, and states it as something it has not done.
    [Test]
    public async Task WritePrune_OfACheckRun_SaysWhatWouldGo()
    {
        var orphan = DependencyPin.Create(DependencyScope.Packages, "Serilog", "3.0.0", "Directory.Packages.props");
        var output = RenderPrune([orphan], removed: false);
        await Assert.That(output).Contains("Serilog 3.0.0 -> to remove");
        await Assert.That(output).Contains("1 pin would be removed.");
    }

    [Test]
    public async Task WritePrune_WithNoOrphan_SaysSo()
        => await Assert.That(RenderPrune([], removed: true)).Contains("something references every pin");

    private static string RenderPrune(IReadOnlyList<DependencyPin> orphans, bool removed)
    {
        using var console = new TestConsole();
        _ = console.Width(200);
        new DependencyReportRenderer(console, new EffectivePolicyResolver(new DependenciesConfig())).WritePrune(orphans, removed);
        return console.Output;
    }

    private static string RenderOverrides(IReadOnlyList<TransitiveOverrideEntry> overrides)
    {
        using var console = new TestConsole();
        _ = console.Width(200);
        new DependencyReportRenderer(console, new EffectivePolicyResolver(new DependenciesConfig())).WriteOverrides(overrides);
        return console.Output;
    }

    private static string Render(DependencyInventory inventory, IReadOnlyList<DependencyScope> scopes)
    {
        // Wide enough that no note wraps: what these tests are about is what the report says, not how a
        // narrow terminal breaks it.
        using var console = new TestConsole();
        _ = console.Width(200);
        var renderer = new DependencyReportRenderer(console, new EffectivePolicyResolver(new DependenciesConfig()));
        renderer.Write(inventory, new HashSet<DependencyScope>(scopes));
        return console.Output;
    }
}
