// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
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
        WriteHook(home, "hook.cs", hook);
        var updater = CreateUpdater(home);

        var pins = updater.DiscoverPins();

        // Walk order: depth-first, each directory's entries ordinally sorted. The versionless directive and
        // the non-family ids contribute nothing.
        var rendered = pins.Select(static p => $"{p.RelativePath}|{p.Id}|{p.VersionText}|{p.Version?.ToNormalizedString()}");
        await Assert.That(rendered).IsEquivalentTo(
        [
            ".buildvana/hooks/hook.cs|Buildvana.Sdk|2.1.40-preview|2.1.40-preview",
            ".buildvana/hooks/hook.cs|Buildvana.Runtime|2.1.40-preview|2.1.40-preview",
            "Directory.Packages.props|Buildvana.Runtime|2.1.40-preview|2.1.40-preview",
            "Directory.Packages.props|Buildvana.Sdk|2.1.40-preview|2.1.40-preview",
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
        foreach (var directory in new[] { "artifacts", ".buildvana-temp", "obj", "node_modules", "ignored", Path.Combine("src", "bin") })
        {
            _ = Directory.CreateDirectory(Path.Combine(home.RootPath, directory));
            home.WriteFile(Path.Combine(directory, "Debris.csproj"), project);
        }

        var updater = CreateUpdater(home);

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
        var updater = CreateUpdater(home);

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
        var updater = CreateUpdater(home);
        var pins = updater.DiscoverPins();

        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        await Assert.That(home.ReadFile("App.csproj"))
            .IsEqualTo(before.Replace("2.1.40-preview", "2.1.41-preview", StringComparison.Ordinal));
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
        var updater = CreateUpdater(home);
        var pins = updater.DiscoverPins();

        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        await Assert.That(home.ReadFile("Directory.Packages.props"))
            .IsEqualTo(before.Replace("2.1.40-preview", "2.1.41-preview", StringComparison.Ordinal));
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
        var updater = CreateUpdater(home);
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
        WriteHook(home, "hook.cs", before);
        var updater = CreateUpdater(home);
        var pins = updater.DiscoverPins();

        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        const string after = """
            #!/usr/bin/env dotnet
            #:sdk Buildvana.Sdk@2.1.41-preview
            #:package Buildvana.Runtime@2.1.41-preview
            #:package Louis@10.0.46

            Console.WriteLine("2.1.40-preview");
            """;
        await Assert.That(home.ReadFile(Path.Combine(".buildvana", "hooks", "hook.cs"))).IsEqualTo(after);
        await Assert.That(lines).IsEquivalentTo(
        [
            "Buildvana.Sdk: 2.1.40-preview -> 2.1.41-preview (.buildvana/hooks/hook.cs)",
            "Buildvana.Runtime: 2.1.40-preview -> 2.1.41-preview (.buildvana/hooks/hook.cs)",
        ]);
    }

    // Only .cs files within the file-based-app scope are read: a directive-bearing file elsewhere is out of
    // scope by the user's own statement, and reading every .cs file would scale discovery with the source tree.
    [Test]
    public async Task DiscoverPins_ReadsOnlyCSharpFilesWithinScope()
    {
        using var home = new TempHome();
        const string app = """
            #:package Buildvana.Runtime@2.1.40-preview

            Console.WriteLine();
            """;
        home.WriteFile("stray.cs", app);
        WriteHook(home, "hook.cs", app);
        var updater = CreateUpdater(home);

        var pins = updater.DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(1);
        await Assert.That(pins[0].RelativePath).IsEqualTo(".buildvana/hooks/hook.cs");
    }

    // The resolved configuration states the scope; a pattern it adds brings the matching files in.
    [Test]
    public async Task DiscoverPins_WithConfiguredPatterns_ExtendsTheScope()
    {
        using var home = new TempHome();
        const string app = """
            #:package Buildvana.Runtime@2.1.40-preview

            Console.WriteLine();
            """;
        home.WriteFile("stray.cs", app);
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, "tools"));
        home.WriteFile(Path.Combine("tools", "tool.cs"), app);
        var updater = CreateUpdater(home, new BuildvanaConfig { FileBasedApps = ["/tools/"] });

        var pins = updater.DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(1);
        await Assert.That(pins[0].RelativePath).IsEqualTo("tools/tool.cs");
    }

    // The factory emits the built-in patterns after the configured ones, and in gitignore syntax the last
    // matching pattern wins — so a configured negation of the hooks scope is inert. The factory composes
    // the scope here, so the test covers the whole path from stated configuration to discovery.
    [Test]
    public async Task DiscoverPins_WithConfiguredNegationOfHooksScope_StillReadsHooks()
    {
        using var home = new TempHome();
        const string app = """
            #:package Buildvana.Runtime@2.1.40-preview

            Console.WriteLine();
            """;
        WriteHook(home, "hook.cs", app);
        var json = new BuildvanaJsonConfig { FileBasedApps = ["!.buildvana/hooks/"] };
        var updater = CreateUpdater(home, BuildvanaConfigFactory.Create(json, null));

        var pins = updater.DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(1);
        await Assert.That(pins[0].RelativePath).IsEqualTo(".buildvana/hooks/hook.cs");
    }

    // Self-update is the tool that repairs a half-updated repository, so a configuration file this bv
    // cannot read degrades the scope to the built-in default with a warning instead of killing the update.
    [Test]
    public async Task DiscoverPins_WhenConfigurationUnreadable_FallsBackToHooksScopeAndWarns()
    {
        using var home = new TempHome();
        const string app = """
            #:package Buildvana.Runtime@2.1.40-preview

            Console.WriteLine();
            """;
        home.WriteFile("stray.cs", app);
        WriteHook(home, "hook.cs", app);
        var reporter = new CaptureReporter();
        var updater = new FamilyPinUpdater(
            home.Provider,
            new Lazy<BuildvanaConfig>(static () => throw new BuildFailedException("unreadable")),
            reporter);

        var pins = updater.DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(1);
        await Assert.That(pins[0].RelativePath).IsEqualTo(".buildvana/hooks/hook.cs");
        var warnings = reporter.Messages.Where(static m => m.Level == MessageLevel.Warning).ToList();
        await Assert.That(warnings.Count).IsEqualTo(1);
        await Assert.That(warnings[0].Message).Contains(".buildvana/hooks");
    }

    // A repository that pins packages under an item name of its own declares it as an additional pin group.
    // A family pin written that way is one like any other, so discovery and stamping both reach it.
    [Test]
    public async Task DiscoverAndStampPins_ReachFamilyPinsUnderAConfiguredItemName()
    {
        using var home = new TempHome();
        const string before = """
            <Project>
              <ItemGroup>
                <BV_PackageVersion Include="Buildvana.Runtime" Version="2.1.40-preview" />
                <BV_PackageVersion Include="Louis" Version="10.0.46" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("PackageVersions.props", before);
        var updater = CreateUpdater(home, ConfigWithGroupItemName("BV_PackageVersion"));

        var pins = updater.DiscoverPins();
        var lines = updater.StampPins(pins, NuGetVersion.Parse("2.1.41-preview"));

        await Assert.That(home.ReadFile("PackageVersions.props"))
            .IsEqualTo(before.Replace("2.1.40-preview", "2.1.41-preview", StringComparison.Ordinal));
        await Assert.That(lines).IsEquivalentTo(
            ["Buildvana.Runtime: 2.1.40-preview -> 2.1.41-preview (PackageVersions.props)"]);
    }

    // Without the group, the same file declares nothing self-update knows how to read.
    [Test]
    public async Task DiscoverPins_WithoutTheGroup_DoesNotSeeAConfiguredItemName()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <BV_PackageVersion Include="Buildvana.Runtime" Version="2.1.40-preview" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("PackageVersions.props", content);

        var pins = CreateUpdater(home).DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(0);
    }

    // MSBuild compares item names case-insensitively, so a group naming a built-in type adds nothing, and
    // the built-in types are not scanned twice.
    [Test]
    public async Task DiscoverPins_WithAGroupNamingABuiltInItemType_ReportsEachPinOnce()
    {
        using var home = new TempHome();
        const string content = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Buildvana.Runtime" Version="2.1.40-preview" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("Directory.Packages.props", content);
        var updater = CreateUpdater(home, ConfigWithGroupItemName("packageversion"));

        var pins = updater.DiscoverPins();

        await Assert.That(pins.Count).IsEqualTo(1);
    }

    private static BuildvanaConfig ConfigWithGroupItemName(string itemName)
    {
        var json = new BuildvanaJsonConfig
        {
            Dependencies = new()
            {
                AdditionalPackages =
                [
                    new() { Caption = "SDK-injected packages", Files = "PackageVersions.props", Items = itemName },
                ],
            },
        };

        return BuildvanaConfigFactory.Create(json, null);
    }

    private static FamilyPinUpdater CreateUpdater(TempHome home, BuildvanaConfig? config = null)
        => new(
            home.Provider,
            new Lazy<BuildvanaConfig>(() => config ?? new BuildvanaConfig()),
            NullReporter.Instance);

    private static void WriteHook(TempHome home, string fileName, string content)
    {
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, ".buildvana", "hooks"));
        home.WriteFile(Path.Combine(".buildvana", "hooks", fileName), content);
    }
}
