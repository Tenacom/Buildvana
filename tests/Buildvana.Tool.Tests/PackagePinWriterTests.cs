// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class PackagePinWriterTests
{
    private const string PropsFile = "Directory.Packages.props";

    private const string TwoPins = """
                                   <Project>
                                     <ItemGroup>
                                       <!-- a comment nothing may touch -->
                                       <PackageVersion Include="Serilog" Version="3.0.0" />
                                       <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                                     </ItemGroup>
                                   </Project>
                                   """;

    private const string TwoPinsWithSerilogMoved = """
                                                   <Project>
                                                     <ItemGroup>
                                                       <!-- a comment nothing may touch -->
                                                       <PackageVersion Include="Serilog" Version="3.1.0" />
                                                       <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                                                     </ItemGroup>
                                                   </Project>
                                                   """;

    private const string TwoDeclarationsOfOneId = """
                                                  <Project>
                                                    <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
                                                      <PackageVersion Include="Serilog" Version="3.0.0" />
                                                    </ItemGroup>
                                                    <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
                                                      <PackageVersion Include="Serilog" Version="3.0.0" />
                                                    </ItemGroup>
                                                  </Project>
                                                  """;

    private const string TwoVersionsOfOneId = """
                                              <Project>
                                                <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
                                                  <PackageVersion Include="Serilog" Version="3.0.0" />
                                                </ItemGroup>
                                                <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
                                                  <PackageVersion Include="Serilog" Version="2.0.0" />
                                                </ItemGroup>
                                              </Project>
                                              """;

    private const string TwoVersionsOfOneIdWithTheOlderMoved = """
                                                               <Project>
                                                                 <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
                                                                   <PackageVersion Include="Serilog" Version="3.0.0" />
                                                                 </ItemGroup>
                                                                 <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
                                                                   <PackageVersion Include="Serilog" Version="2.1.0" />
                                                                 </ItemGroup>
                                                               </Project>
                                                               """;

    [Test]
    public async Task Write_SplicesTheVersionAndNothingElse()
    {
        using var home = new TempHome();
        home.WriteFile(PropsFile, TwoPins);
        Write(home, Moving("Serilog", "3.0.0", "3.1.0", PropsFile));
        await Assert.That(home.ReadFile(PropsFile)).IsEqualTo(TwoPinsWithSerilogMoved);
    }

    // MSBuild evaluated both declarations as one pin, and the splice moves both.
    [Test]
    public async Task Write_MovesEveryDeclarationThatStatesTheSameVersion()
    {
        using var home = new TempHome();
        home.WriteFile(PropsFile, TwoDeclarationsOfOneId);
        Write(home, Moving("Serilog", "3.0.0", "3.1.0", PropsFile));
        await Assert.That(home.ReadFile(PropsFile)).DoesNotContain("3.0.0");
        await Assert.That(home.ReadFile(PropsFile).Split("3.1.0").Length).IsEqualTo(3);
    }

    // One id, two declarations, two versions: MSBuild evaluated one of them, and only that one moves.
    [Test]
    public async Task Write_LeavesADeclarationStatingAnotherVersionAlone()
    {
        using var home = new TempHome();
        home.WriteFile(PropsFile, TwoVersionsOfOneId);
        Write(home, Moving("Serilog", "2.0.0", "2.1.0", PropsFile));
        await Assert.That(home.ReadFile(PropsFile)).IsEqualTo(TwoVersionsOfOneIdWithTheOlderMoved);
    }

    // Every pin came out of this file, so a file stating none of them changed under us. Reporting the write
    // would leave a pin behind, and a report saying it had moved.
    [Test]
    public async Task Write_WhenTheFileStatesNoPinThatMoves_Fails()
    {
        using var home = new TempHome();
        home.WriteFile(PropsFile, TwoPins);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(() => Write(home, Moving("Serilog", "2.0.0", "2.1.0", PropsFile))).Throws<BuildFailedException>();
        await Assert.That(home.ReadFile(PropsFile)).IsEqualTo(TwoPins);
    }

    [Test]
    public async Task Write_SplicesTheVersionOfADirective()
    {
        const string app = """
                           #:package Serilog@3.0.0
                           #:package Newtonsoft.Json@13.0.3

                           Console.WriteLine("hello");
                           """;

        using var home = new TempHome();
        home.WriteFile("tools/build.cs", app);
        Write(home, Moving("Serilog", "3.0.0", "3.1.0", "tools/build.cs", itemType: null));
        await Assert.That(home.ReadFile("tools/build.cs")).Contains("#:package Serilog@3.1.0");
        await Assert.That(home.ReadFile("tools/build.cs")).Contains("#:package Newtonsoft.Json@13.0.3");
    }

    [Test]
    public async Task Write_LeavesAPinThatDoesNotMoveAlone()
    {
        using var home = new TempHome();
        home.WriteFile(PropsFile, TwoPins);
        var pin = DependencyPin.Create(DependencyScope.Packages, "Serilog", "3.0.0", PropsFile) with { ItemType = "PackageVersion" };
        Write(home, new PinResolution { Pin = pin, Policy = Policy(), State = PinResolutionState.UpToDate });
        await Assert.That(home.ReadFile(PropsFile)).IsEqualTo(TwoPins);
    }

    private static PackageUpdatePolicy Policy()
    {
        _ = PackageUpdatePolicy.TryParse("minor", out var policy);
        return policy;
    }

    private static PinResolution Moving(string id, string from, string to, string file, string? itemType = "PackageVersion")
        => new()
        {
            Pin = DependencyPin.Create(DependencyScope.Packages, id, from, file) with { ItemType = itemType },
            Policy = Policy(),
            State = PinResolutionState.Updated,
            Target = NuGetVersion.Parse(to),
        };

    private static void Write(TempHome home, params PinResolution[] pins)
        => new PackagePinWriter(home.Provider, NullReporter.Instance).Write(pins);
}
