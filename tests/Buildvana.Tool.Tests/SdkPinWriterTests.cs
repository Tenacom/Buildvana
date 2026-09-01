// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Json;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class SdkPinWriterTests
{
    private const string GlobalJson = """
                                      {
                                        "sdk": {
                                          "version": "10.0.100"
                                        },
                                        "msbuild-sdks": {
                                          "Contoso.Sdk": "1.0.0",
                                          "Microsoft.Build.NoTargets": "3.7.134"
                                        }
                                      }
                                      """;

    [Test]
    public async Task Write_MovesAnMsBuildSdksEntry()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", GlobalJson);
        Write(home, Moving("Contoso.Sdk", "1.0.0", "1.1.0", "global.json"));
        var written = home.ReadFile("global.json");
        await Assert.That(written).Contains("\"Contoso.Sdk\": \"1.1.0\"");
        await Assert.That(written).Contains("\"Microsoft.Build.NoTargets\": \"3.7.134\"");
        await Assert.That(written).Contains("\"version\": \"10.0.100\"");
    }

    [Test]
    public async Task Write_MovesTheVersionOfAnSdkDirective()
    {
        const string app = """
                           #:sdk Contoso.Sdk@1.0.0
                           #:package Serilog@3.0.0

                           Console.WriteLine("hello");
                           """;

        using var home = new TempHome();
        home.WriteFile("tools/build.cs", app);
        Write(home, Moving("Contoso.Sdk", "1.0.0", "1.1.0", "tools/build.cs"));
        var written = home.ReadFile("tools/build.cs");
        await Assert.That(written).Contains("#:sdk Contoso.Sdk@1.1.0");
        await Assert.That(written).Contains("#:package Serilog@3.0.0");
    }

    [Test]
    public async Task Write_LeavesAPinThatDoesNotMoveAlone()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", GlobalJson);
        var pin = DependencyPin.Create(DependencyScope.Sdks, "Contoso.Sdk", "1.0.0", "global.json");
        Write(home, new PinResolution { Pin = pin, Policy = Policy(), State = PinResolutionState.UpToDate });
        await Assert.That(home.ReadFile("global.json")).IsEqualTo(GlobalJson);
    }

    // Every pin came out of this file, so a file stating none of them changed under us. Reporting the write
    // would leave a pin behind, and a report saying it had moved.
    [Test]
    public async Task Write_WhenTheFileStatesNoPinThatMoves_Fails()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", GlobalJson);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(() => Write(home, Moving("Fabrikam.Sdk", "1.0.0", "1.1.0", "global.json"))).Throws<BuildFailedException>();
        await Assert.That(home.ReadFile("global.json")).IsEqualTo(GlobalJson);
    }

    private static PackageUpdatePolicy Policy()
    {
        _ = PackageUpdatePolicy.TryParse("minor", out var policy);
        return policy;
    }

    private static PinResolution Moving(string id, string from, string to, string file)
        => new()
        {
            Pin = DependencyPin.Create(DependencyScope.Sdks, id, from, file),
            Policy = Policy(),
            State = PinResolutionState.Updated,
            Target = NuGetVersion.Parse(to),
        };

    private static void Write(TempHome home, params PinResolution[] pins)
        => new SdkPinWriter(home.Provider, new JsonHelper(), NullReporter.Instance).Write(pins);
}
