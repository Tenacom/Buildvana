// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;
using Spectre.Console.Testing;

// The update report of `bv dependencies update`, which says what a run made of every pin. The pins here are
// resolutions built by hand: what the resolver decides is its own tests' business.
internal sealed class DependencyUpdateReportTests
{
    private static readonly DependencyScope[] AllScopes =
        [DependencyScope.NetSdk, DependencyScope.Sdks, DependencyScope.Tools, DependencyScope.Packages];

    [Test]
    public async Task WriteUpdate_StatesTheTargetAndWhatLiesBeyondIt()
    {
        var resolution = Packages(Moving("Serilog", "3.0.0", "3.1.0", latestStable: "4.0.0"));
        var output = Render(resolution, [DependencyScope.Packages]);
        await Assert.That(output).Contains("Serilog 3.0.0 (minor) -> 3.1.0 (latest: 4.0.0)");
        await Assert.That(output).Contains("1 pin would change.");
    }

    // The report at the width of a CI log. A name and a version stay whole, whatever it costs the alignment:
    // a line breaks between words, where a column layout used to break inside them.
    [Test]
    public async Task WriteUpdate_AtEightyColumns_BreaksNoNameAndNoVersion()
    {
        var pin = DependencyPin.Create(DependencyScope.Packages, "Microsoft.Build.NoTargets", "3.7.134", "Directory.Packages.props");
        var resolution = Packages(new PinResolution
        {
            Pin = pin,
            Policy = Policy("minor"),
            State = PinResolutionState.UpToDate,
            LatestStable = NuGetVersion.Parse("3.7.134"),
            LatestPreview = NuGetVersion.Parse("11.0.100-preview.7.26381.103"),
        });

        var output = Render(resolution, [DependencyScope.Packages], listUpToDate: true, width: 80);
        await Assert.That(output).Contains("Microsoft.Build.NoTargets 3.7.134 (minor) -> up to date");
        await Assert.That(output).Contains("11.0.100-preview.7.26381.103");
    }

    [Test]
    public async Task WriteUpdate_OfAnAppliedRun_SaysWhatChanged()
    {
        var resolution = Packages(Moving("Serilog", "3.0.0", "3.1.0"));
        var output = Render(resolution, [DependencyScope.Packages], applied: true);
        await Assert.That(output).Contains("1 pin changed.");
        await Assert.That(output).DoesNotContain("would change");
    }

    [Test]
    public async Task WriteUpdate_LeavesOutAPinThatIsUpToDate()
    {
        var resolution = Packages(UpToDate("Serilog", "4.0.0"));
        var output = Render(resolution, [DependencyScope.Packages]);
        await Assert.That(output).DoesNotContain("Serilog");
        await Assert.That(output).Contains("No pin would change.");
        await Assert.That(output).Contains("1 pin up to date, not listed.");
    }

    [Test]
    public async Task WriteUpdate_AskedForEveryPin_ListsTheUpToDateOnes()
    {
        var resolution = Packages(UpToDate("Serilog", "4.0.0"));
        var output = Render(resolution, [DependencyScope.Packages], listUpToDate: true);
        await Assert.That(output).Contains("Serilog 4.0.0 (minor) -> up to date (latest: 4.0.0)");
        await Assert.That(output).DoesNotContain("not listed");
    }

    [Test]
    public async Task WriteUpdate_StatesWhyAPinDoesNotMove()
    {
        var pin = DependencyPin.Create(DependencyScope.Packages, "Serilog", "[3.0.0]", "Directory.Packages.props");
        var resolution = Packages(new PinResolution
        {
            Pin = pin,
            Policy = Policy("minor"),
            State = PinResolutionState.Unmanaged,
            Note = PinNotes.Unmanaged(pin.Management),
        });

        var output = Render(resolution, [DependencyScope.Packages]);
        await Assert.That(output).Contains("Serilog [3.0.0] (minor) -> not managed");
        await Assert.That(output).Contains("not managed: one version in brackets");
    }

    // The baseline has news of its own: a setting an apply run writes, whether or not the version moves.
    [Test]
    public async Task WriteUpdate_ListsTheNetSdkWhenOnlyAllowPrereleaseWouldChange()
    {
        var resolution = new DependencyResolution
        {
            NetSdk = new NetSdkResolution
            {
                Pin = NetSdkPin.Create("10.0.100", allowPrerelease: null),
                Policy = NetSdkPolicy("major"),
                State = PinResolutionState.UpToDate,
                WritesAllowPrerelease = true,
                Note = "global.json states allowPrerelease as unstated, where the policy says False",
            },
        };

        var output = Render(resolution, [DependencyScope.NetSdk]);
        await Assert.That(output).Contains("10.0.100");
        await Assert.That(output).Contains("allowPrerelease");
        await Assert.That(output).Contains("1 pin would change.");
    }

    [Test]
    public async Task WriteUpdate_OfASelectedScopeWithNoPin_SaysSo()
    {
        var output = Render(new DependencyResolution(), [DependencyScope.Tools]);
        await Assert.That(output).Contains("nothing pinned");
    }

    [Test]
    public async Task WriteUpdate_SaysNothingAboutAnUnselectedScope()
    {
        var resolution = Packages(Moving("Serilog", "3.0.0", "3.1.0"));
        var output = Render(resolution, [DependencyScope.Packages]);
        await Assert.That(output).DoesNotContain(".NET SDK");
        await Assert.That(output).DoesNotContain("local tools");
    }

    [Test]
    public async Task WriteUpdate_StatesAGroupsPinsUnderItsCaption()
    {
        var moving = Moving("StyleCop.Analyzers", "1.2.0", "1.3.0");
        var resolution = Packages(moving with { Pin = moving.Pin with { GroupCaption = "SDK package injections" } });
        var output = Render(resolution, [DependencyScope.Packages]);
        await Assert.That(output).Contains("NuGet packages: SDK package injections");
        await Assert.That(output).Contains("StyleCop.Analyzers");
    }

    [Test]
    public async Task WriteUpdate_OfEveryScope_StatesThemAll()
    {
        var resolution = new DependencyResolution
        {
            NetSdk = new NetSdkResolution
            {
                Pin = NetSdkPin.Create("10.0.100", allowPrerelease: false),
                Policy = NetSdkPolicy("major"),
                State = PinResolutionState.Updated,
                WritesAllowPrerelease = false,
                Target = NuGetVersion.Parse("10.0.201"),
            },
            Sdks = [Moving("Contoso.Sdk", "1.0.0", "1.1.0")],
            Tools = [Moving("ngbv", "0.5.1", "0.6.0")],
            Packages = [Moving("Serilog", "3.0.0", "3.1.0")],
        };

        var output = Render(resolution, AllScopes);
        await Assert.That(output).Contains("10.0.201");
        await Assert.That(output).Contains("Contoso.Sdk");
        await Assert.That(output).Contains("ngbv");
        await Assert.That(output).Contains("Serilog");
    }

    private static PackageUpdatePolicy Policy(string text)
    {
        _ = PackageUpdatePolicy.TryParse(text, out var policy);
        return policy;
    }

    private static NetSdkUpdatePolicy NetSdkPolicy(string text)
    {
        _ = NetSdkUpdatePolicy.TryParse(text, out var policy);
        return policy;
    }

    private static PinResolution Moving(string id, string from, string to, string? latestStable = null)
        => new()
        {
            Pin = DependencyPin.Create(DependencyScope.Packages, id, from, "Directory.Packages.props"),
            Policy = Policy("minor"),
            State = PinResolutionState.Updated,
            Target = NuGetVersion.Parse(to),
            LatestStable = NuGetVersion.Parse(latestStable ?? to),
        };

    private static PinResolution UpToDate(string id, string version)
        => new()
        {
            Pin = DependencyPin.Create(DependencyScope.Packages, id, version, "Directory.Packages.props"),
            Policy = Policy("minor"),
            State = PinResolutionState.UpToDate,
            LatestStable = NuGetVersion.Parse(version),
        };

    private static DependencyResolution Packages(params PinResolution[] pins) => new() { Packages = pins };

    private static string Render(
        DependencyResolution resolution,
        IReadOnlyList<DependencyScope> scopes,
        bool listUpToDate = false,
        bool applied = false,
        int width = 200)
    {
        // Wide enough by default that no line wraps: what most of these tests are about is what the report
        // says, not how a narrow terminal breaks it.
        using var console = new TestConsole();
        _ = console.Width(width);
        var renderer = new DependencyReportRenderer(console, new EffectivePolicyResolver(new DependenciesConfig()));
        renderer.WriteUpdate(resolution, new HashSet<DependencyScope>(scopes), listUpToDate, applied);
        return console.Output;
    }
}
