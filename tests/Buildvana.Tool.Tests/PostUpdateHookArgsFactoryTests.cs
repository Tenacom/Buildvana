// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.Configuration;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Services.Hooks;
using NuGet.Versioning;

internal sealed class PostUpdateHookArgsFactoryTests
{
    [Test]
    public async Task Create_StatesWhatTheRunMadeOfEveryScope()
    {
        using var home = new TempHome();
        var resolution = new DependencyResolution
        {
            NetSdk = new NetSdkResolution
            {
                Pin = NetSdkPin.Create("10.0.100", allowPrerelease: false),
                Policy = NetSdkPolicy(),
                State = PinResolutionState.Updated,
                WritesAllowPrerelease = false,
                Target = NuGetVersion.Parse("10.0.201"),
            },
            Tools = [Moving("ngbv", "0.5.1", "0.6.0", DependencyScope.Tools)],
            Packages = [Moving("Serilog", "3.0.0", "3.1.0", DependencyScope.Packages)],
        };

        var args = Create(home, resolution, check: true);
        await Assert.That(args.Check).IsTrue();
        await Assert.That(args.NetSdk!.Id).IsNull();
        await Assert.That(args.NetSdk.DeclaringFile).IsEqualTo("global.json");
        await Assert.That(args.NetSdk.Target).IsEqualTo("10.0.201");
        await Assert.That(args.NetSdk.State).IsEqualTo(DependencyResultState.Updated);
        await Assert.That(args.Tools.Single().Id).IsEqualTo("ngbv");
        await Assert.That(args.Packages.Single().Id).IsEqualTo("Serilog");
        await Assert.That(args.Packages.Single().CurrentVersion).IsEqualTo("3.0.0");
        await Assert.That(args.Packages.Single().Policy).IsEqualTo("minor");
        await Assert.That(args.Sdks).IsEmpty();
        await Assert.That(args.AdditionalPackages).IsEmpty();
    }

    // A group's pins are a section of their own, under the caption configuration gives the group.
    [Test]
    public async Task Create_StatesAGroupsPinsUnderItsCaption()
    {
        using var home = new TempHome();
        var grouped = Moving("StyleCop.Analyzers", "1.2.0", "1.3.0", DependencyScope.Packages);
        var resolution = new DependencyResolution
        {
            Packages =
            [
                Moving("Serilog", "3.0.0", "3.1.0", DependencyScope.Packages),
                grouped with { Pin = grouped.Pin with { GroupCaption = "SDK package injections" } },
            ],
        };

        var args = Create(home, resolution, check: false);
        await Assert.That(args.Packages.Single().Id).IsEqualTo("Serilog");
        await Assert.That(args.AdditionalPackages.Single().Caption).IsEqualTo("SDK package injections");
        await Assert.That(args.AdditionalPackages.Single().Results.Single().Id).IsEqualTo("StyleCop.Analyzers");
    }

    // The args reach a hook as JSON written by the source-generated context, which is what makes them
    // readable in a file-based app, where reflection-based serialization is off.
    [Test]
    public async Task Create_ProducesArgsTheHookContractCanWrite()
    {
        using var home = new TempHome();
        var resolution = new DependencyResolution { Tools = [Moving("ngbv", "0.5.1", "0.6.0", DependencyScope.Tools)] };
        var args = Create(home, resolution, check: false);
        var json = JsonSerializer.Serialize(args, BuildvanaJsonContext.Default.PostUpdateHookArgs);
        await Assert.That(json).Contains("\"check\": false");
        await Assert.That(json).Contains("\"id\": \"ngbv\"");
        await Assert.That(json).Contains("\"state\": \"Updated\"");
    }

    // The overrides in the args are the files as they stand: an apply run has just rewritten them, and a
    // check run reports what the last apply run wrote.
    [Test]
    public async Task Create_StatesTheOverridesInEffect()
    {
        const string overrides = """
                                 <Project>
                                   <ItemGroup>
                                     <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                                   </ItemGroup>
                                 </Project>
                                 """;

        using var home = new TempHome();
        home.WriteFile("Directory.TransitiveOverrides.props", overrides);
        var args = Create(home, new DependencyResolution(), check: false);
        var entry = args.Overrides.Single();
        await Assert.That(entry.PackageId).IsEqualTo("Newtonsoft.Json");
        await Assert.That(entry.Version).IsEqualTo("13.0.3");
        await Assert.That(entry.DeclaringFile).IsEqualTo("Directory.TransitiveOverrides.props");
    }

    [Test]
    public async Task Create_WithNoOverrideFile_StatesNone()
    {
        using var home = new TempHome();
        await Assert.That(Create(home, new DependencyResolution(), check: false).Overrides).IsEmpty();
    }

    private static PackageUpdatePolicy Policy()
    {
        _ = PackageUpdatePolicy.TryParse("minor", out var policy);
        return policy;
    }

    private static NetSdkUpdatePolicy NetSdkPolicy()
    {
        _ = NetSdkUpdatePolicy.TryParse("major", out var policy);
        return policy;
    }

    private static PinResolution Moving(string id, string from, string to, DependencyScope scope)
        => new()
        {
            Pin = DependencyPin.Create(scope, id, from, "Directory.Packages.props"),
            Policy = Policy(),
            State = PinResolutionState.Updated,
            Target = NuGetVersion.Parse(to),
        };

    private static PostUpdateHookArgs Create(TempHome home, DependencyResolution resolution, bool check)
        => new PostUpdateHookArgsFactory(
            home.Provider,
            new BuildvanaJsonConfigProvider(home.Provider),
            new BuildvanaConfig(),
            new SidecarReader(home.Provider, new CaptureReporter()))
            .Create(resolution, check);
}
