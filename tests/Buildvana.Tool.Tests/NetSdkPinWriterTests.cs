// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Json;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class NetSdkPinWriterTests
{
    private const string WithoutAllowPrerelease = """
                                                  {
                                                    "sdk": {
                                                      "version": "10.0.100"
                                                    }
                                                  }
                                                  """;

    private const string WithAllowPrerelease = """
                                               {
                                                 "sdk": {
                                                   "version": "10.0.100",
                                                   "allowPrerelease": true
                                                 }
                                               }
                                               """;

    [Test]
    public async Task Write_MovesTheBaseline()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", WithoutAllowPrerelease);
        Write(home, Resolution("10.0.100", stated: null, target: "10.0.201", policy: "major"));
        await Assert.That(home.ReadFile("global.json")).Contains("\"version\": \"10.0.201\"");
    }

    [Test]
    public async Task Write_AddsAllowPrereleaseWhenTheFileStatesNone()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", WithoutAllowPrerelease);
        Write(home, Resolution("10.0.100", stated: null, target: null, policy: "major"));
        await Assert.That(home.ReadFile("global.json")).Contains("\"allowPrerelease\": false");
    }

    [Test]
    public async Task Write_MovesAllowPrereleaseWhenItDisagreesWithThePolicy()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", WithAllowPrerelease);
        Write(home, Resolution("10.0.100", stated: true, target: null, policy: "major"));
        await Assert.That(home.ReadFile("global.json")).Contains("\"allowPrerelease\": false");
    }

    [Test]
    public async Task Write_UnderAPrereleasePolicy_StatesAllowPrereleaseAsTrue()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", WithoutAllowPrerelease);
        Write(home, Resolution("10.0.100", stated: null, target: null, policy: "major-"));
        await Assert.That(home.ReadFile("global.json")).Contains("\"allowPrerelease\": true");
    }

    [Test]
    public async Task Write_WithNothingToDo_LeavesTheFileAlone()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", WithAllowPrerelease);
        Write(home, Resolution("10.0.100", stated: true, target: null, policy: "major-"));
        await Assert.That(home.ReadFile("global.json")).IsEqualTo(WithAllowPrerelease);
    }

    private static NetSdkResolution Resolution(string version, bool? stated, string? target, string policy)
    {
        _ = NetSdkUpdatePolicy.TryParse(policy, out var parsed);
        var pin = NetSdkPin.Create(version, stated);
        return new NetSdkResolution
        {
            Pin = pin,
            Policy = parsed,
            State = target is null ? PinResolutionState.UpToDate : PinResolutionState.Updated,
            WritesAllowPrerelease = pin.AllowPrerelease != parsed.AllowPrerelease,
            Target = target is null ? null : NuGetVersion.Parse(target),
        };
    }

    private static void Write(TempHome home, NetSdkResolution resolution)
        => new NetSdkPinWriter(home.Provider, new JsonHelper(), NullReporter.Instance).Write(resolution);
}
