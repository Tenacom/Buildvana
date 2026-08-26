// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Buildvana.Tool.Services;
using NuGet.Versioning;

internal sealed class FamilyPinUpdaterTests
{
    [Test]
    public async Task DiscoverPins_FindsFamilyPinsAcrossFileKinds()
    {
        using var home = new TempHome();
        const string packagesProps = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Buildvana.Runtime" Version="2.1.40-preview" />
                <PackageVersion Include="Louis" Version="10.0.46" />
                <GlobalPackageReference Include="Buildvana.Sdk" Version="2.1.40-preview" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("Directory.Packages.props", packagesProps);
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="buildvana.runtime" Version="2.1.39-preview" />
              </ItemGroup>
            </Project>
            """;
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, "src"));
        home.WriteFile(Path.Combine("src", "Foo.csproj"), project);
        const string hook = """
            #:sdk Buildvana.Sdk@2.1.40-preview
            #:package Buildvana.Runtime@2.1.40-preview
            #:package Louis@10.0.46
            #:package Buildvana.Runtime

            Console.WriteLine();
            """;
        home.WriteFile("hook.cs", hook);
        var updater = new FamilyPinUpdater(home.Provider);

        var pins = updater.DiscoverPins();

        // Walk order: depth-first, each directory's entries ordinally sorted. The versionless directive and
        // the non-family ids contribute nothing.
        var rendered = pins.Select(static p => $"{p.RelativePath}|{p.Id}|{p.VersionText}|{p.Version?.ToNormalizedString()}");
        await Assert.That(rendered).IsEquivalentTo(
        [
            "Directory.Packages.props|Buildvana.Runtime|2.1.40-preview|2.1.40-preview",
            "Directory.Packages.props|Buildvana.Sdk|2.1.40-preview|2.1.40-preview",
            "hook.cs|Buildvana.Sdk|2.1.40-preview|2.1.40-preview",
            "hook.cs|Buildvana.Runtime|2.1.40-preview|2.1.40-preview",
            "src/Foo.csproj|buildvana.runtime|2.1.39-preview|2.1.39-preview",
        ]);
    }

    [Test]
    public async Task DiscoverPins_SkipsExcludedAndGitignoredDirectories()
    {
        using var home = new TempHome();
        const string project = """
            <Project>
              <ItemGroup>
                <PackageReference Include="Buildvana.Runtime" Version="2.1.40-preview" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("Root.csproj", project);
        home.WriteFile(".gitignore", "/ignored/\n");
        foreach (var directory in new[] { "artifacts", ".buildvana-temp", "obj", "node_modules", "ignored", @"src\bin" })
        {
            _ = Directory.CreateDirectory(Path.Combine(home.RootPath, directory));
            home.WriteFile(Path.Combine(directory, "Debris.csproj"), project);
        }

        var updater = new FamilyPinUpdater(home.Provider);

        var pins = updater.DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(1);
        await Assert.That(pins[0].RelativePath).IsEqualTo("Root.csproj");
    }

    [Test]
    [Arguments("$(BuildvanaVersion)")]
    [Arguments("[2.1.40-preview]")]
    [Arguments("2.1.*")]
    public async Task DiscoverPins_WithNonLiteralVersion_LeavesVersionNull(string versionText)
    {
        using var home = new TempHome();
        var project = $"""
            <Project>
              <ItemGroup>
                <PackageVersion Include="Buildvana.Runtime" Version="{versionText}" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("Directory.Packages.props", project);
        var updater = new FamilyPinUpdater(home.Provider);

        var pins = updater.DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(1);
        await Assert.That(pins[0].VersionText).IsEqualTo(versionText);
        await Assert.That(pins[0].Version).IsNull();
    }

    // The splice must be surgical: comments, attribute order, quoting style, and the non-family pin all
    // survive byte for byte, with only the family pin's version text replaced.
    [Test]
    public async Task StampPins_RewritesLiteralPins_PreservingEverythingElse()
    {
        using var home = new TempHome();
        const string before = """
            <Project>
              <!-- pinned for hooks -->
              <ItemGroup>
                <PackageReference Version="2.1.40-preview" Include="Buildvana.Runtime" />
                <PackageReference Include='Louis' Version='10.0.46' />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("App.csproj", before);
        var updater = new FamilyPinUpdater(home.Provider);
        var pins = updater.DiscoverPins();

        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        await Assert.That(home.ReadFile("App.csproj")).IsEqualTo(before.Replace("2.1.40-preview", "2.1.41-preview", StringComparison.Ordinal));
        await Assert.That(lines).IsEquivalentTo(["Buildvana.Runtime: 2.1.40-preview -> 2.1.41-preview (App.csproj)"]);
    }

    // MSBuild does not trim a Version child element's text, so the raw value includes the surrounding
    // whitespace; the stamp replaces the version alone and keeps the whitespace.
    [Test]
    public async Task StampPins_WithVersionChildElement_PreservesSurroundingWhitespace()
    {
        using var home = new TempHome();
        const string before = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Buildvana.Runtime">
                  <Version>
                    2.1.40-preview
                  </Version>
                </PackageVersion>
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("Directory.Packages.props", before);
        var updater = new FamilyPinUpdater(home.Provider);
        var pins = updater.DiscoverPins();

        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        await Assert.That(home.ReadFile("Directory.Packages.props")).IsEqualTo(before.Replace("2.1.40-preview", "2.1.41-preview", StringComparison.Ordinal));
        await Assert.That(lines).IsEquivalentTo(["Buildvana.Runtime: 2.1.40-preview -> 2.1.41-preview (Directory.Packages.props)"]);
    }

    [Test]
    public async Task StampPins_LeavesNonLiteralAndEqualPinsAlone()
    {
        using var home = new TempHome();
        const string before = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Buildvana.Runtime" Version="$(BuildvanaVersion)" />
                <PackageVersion Include="Buildvana.Sdk" Version="2.1.41-preview+g0123abc" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("Directory.Packages.props", before);
        var updater = new FamilyPinUpdater(home.Provider);
        var pins = updater.DiscoverPins();

        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        await Assert.That(home.ReadFile("Directory.Packages.props")).IsEqualTo(before);
        await Assert.That(lines).IsEquivalentTo(
        [
            "Buildvana.Runtime: $(BuildvanaVersion) (Directory.Packages.props, left alone)",
            "Buildvana.Sdk: 2.1.41-preview (Directory.Packages.props, unchanged)",
        ]);
    }

    // The version literal in the app's own code is the acid test: only directive versions may change.
    [Test]
    public async Task StampPins_RewritesDirectiveVersionsOnly()
    {
        using var home = new TempHome();
        const string before = """
            #!/usr/bin/env dotnet
            #:sdk Buildvana.Sdk@2.1.40-preview
            #:package Buildvana.Runtime@2.1.40-preview
            #:package Louis@10.0.46

            Console.WriteLine("2.1.40-preview");
            """;
        home.WriteFile("hook.cs", before);
        var updater = new FamilyPinUpdater(home.Provider);
        var pins = updater.DiscoverPins();

        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        const string after = """
            #!/usr/bin/env dotnet
            #:sdk Buildvana.Sdk@2.1.41-preview
            #:package Buildvana.Runtime@2.1.41-preview
            #:package Louis@10.0.46

            Console.WriteLine("2.1.40-preview");
            """;
        await Assert.That(home.ReadFile("hook.cs")).IsEqualTo(after);
        await Assert.That(lines).IsEquivalentTo(
        [
            "Buildvana.Sdk: 2.1.40-preview -> 2.1.41-preview (hook.cs)",
            "Buildvana.Runtime: 2.1.40-preview -> 2.1.41-preview (hook.cs)",
        ]);
    }
}
