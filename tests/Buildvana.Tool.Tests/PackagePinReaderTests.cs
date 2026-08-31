// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Tool.Services.Dependencies;

// The reader turns what MSBuild evaluated into pins, and asks each declaring file whether it states the
// version itself, so every test writes the file its items claim to come from.
internal sealed class PackagePinReaderTests
{
    private const string ProjectFileName = "src/App/App.csproj";

    [Test]
    public async Task Read_StatesOnePinPerEvaluatedItem()
    {
        const string project = """
                               <Project>
                                 <ItemGroup>
                                   <PackageVersion Include="Serilog" Version="4.0.0" UpdatePolicy="patch-" />
                                   <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, project);
        var pins = Read(
            home,
            Dump(
                home,
                Item("PackageVersion", "Serilog", "4.0.0", updatePolicy: "patch-"),
                Item("PackageReference", "Newtonsoft.Json", "13.0.3")));

        await Assert.That(pins.Select(static pin => pin.ItemType + " " + pin.Id + " " + pin.VersionText))
            .IsEquivalentTo(["PackageVersion Serilog 4.0.0", "PackageReference Newtonsoft.Json 13.0.3"]);
        await Assert.That(pins.Select(static pin => pin.Management).Distinct().Single()).IsEqualTo(PinManagement.Managed);
        await Assert.That(pins.Single(static pin => pin.Id == "Serilog").MetadataPolicy).IsEqualTo("patch-");
        await Assert.That(pins[0].DeclaringFile).IsEqualTo(ProjectFileName);
        await Assert.That(pins[0].Scope).IsEqualTo(DependencyScope.Packages);
    }

    // Ten projects sharing one Directory.Build.props reference declare one pin, not ten: the file that
    // states it is what an update would edit.
    [Test]
    public async Task Read_OfOneDeclarationEvaluatedTwice_StatesOnePin()
    {
        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, OneItemProject("PackageReference", "Serilog", "4.0.0"));
        var item = Item("PackageReference", "Serilog", "4.0.0");
        await Assert.That(Read(home, Dump(home, item), Dump(home, item)).Count).IsEqualTo(1);
    }

    // Two declarations of one id, one per target framework, are two pins: their version texts differ, and
    // each moves on its own.
    [Test]
    public async Task Read_OfTwoVersionsOfOneId_StatesTwoPins()
    {
        const string project = """
                               <Project>
                                 <ItemGroup>
                                   <PackageReference Include="Serilog" Version="4.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
                                   <PackageReference Include="Serilog" Version="4.1.0" Condition="'$(TargetFramework)' == 'net10.0'" />
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, project);
        var pins = Read(
            home,
            Dump(home, Item("PackageReference", "Serilog", "4.0.0")),
            Dump(home, Item("PackageReference", "Serilog", "4.1.0")));

        await Assert.That(pins.Select(static pin => pin.VersionText)).IsEquivalentTo(["4.0.0", "4.1.0"]);
    }

    [Test]
    public async Task Read_LeavesOutWhatIsNotTheRepositorysToMove()
    {
        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, OneItemProject("PackageReference", "Serilog", "4.0.0"));
        var pins = Read(
            home,
            Dump(
                home,
                Item("PackageReference", "Serilog", "4.0.0"),
                Item("PackageReference", "Microsoft.NET.ILLink.Tasks", "10.0.0", isImplicitlyDefined: true),
                Item("PackageVersion", "Buildvana.Runtime", "2.1.0"),
                Item("PackageReference", "Spectre.Console", null)));

        await Assert.That(pins.Single().Id).IsEqualTo("Serilog");
    }

    // The evaluated version of Version="$(SerilogVersion)" is exact, and the file states an indirection its
    // author wanted: comparing the two is what tells a literal from a property.
    [Test]
    public async Task Read_OfAVersionStatedThroughAProperty_IsIndirect()
    {
        const string project = """
                               <Project>
                                 <PropertyGroup>
                                   <SerilogVersion>4.0.0</SerilogVersion>
                                 </PropertyGroup>
                                 <ItemGroup>
                                   <PackageReference Include="Serilog" Version="$(SerilogVersion)" />
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, project);
        var pins = Read(home, Dump(home, Item("PackageReference", "Serilog", "4.0.0")));
        await Assert.That(pins.Single().Management).IsEqualTo(PinManagement.IndirectVersion);
    }

    // A version applied from elsewhere through Update="..." is attributed to the file that included it,
    // where no literal version lives.
    [Test]
    public async Task Read_OfAVersionAppliedByAnUpdate_IsIndirect()
    {
        const string project = """
                               <Project>
                                 <ItemGroup>
                                   <PackageReference Update="Serilog" Version="4.0.0" />
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, project);
        var pins = Read(home, Dump(home, Item("PackageReference", "Serilog", "4.0.0")));
        await Assert.That(pins.Single().Management).IsEqualTo(PinManagement.IndirectVersion);
    }

    [Test]
    public async Task Read_OfAVersionOverride_SaysSo()
    {
        const string project = """
                               <Project>
                                 <ItemGroup>
                                   <PackageReference Include="Serilog" VersionOverride="4.1.0" />
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, project);
        var item = Item("PackageReference", "Serilog", null, versionOverride: "4.1.0");
        var pin = Read(home, Dump(home, item)).Single();
        await Assert.That(pin.Management).IsEqualTo(PinManagement.VersionOverride);
        await Assert.That(pin.VersionText).IsEqualTo("4.1.0");
    }

    [Test]
    public async Task Read_OfAnUnmanagedVersionForm_KeepsTheFormsReason()
    {
        using var home = new TempHomeDirectory();
        Write(home, ProjectFileName, OneItemProject("PackageVersion", "Serilog", "[4.0.0]"));
        var pins = Read(home, Dump(home, Item("PackageVersion", "Serilog", "[4.0.0]")));
        await Assert.That(pins.Single().Management).IsEqualTo(PinManagement.BracketExactVersion);
    }

    // A Directory.Packages.props above the repository, or one a package supplies, is not the repository's
    // to edit.
    [Test]
    public async Task Read_LeavesOutAnItemDeclaredOutsideTheRepository()
    {
        using var home = new TempHomeDirectory();
        var dump = new PackagePinDump
        {
            ProjectFullPath = home.GetFullPath(ProjectFileName),
            Items = [Item("PackageVersion", "Serilog", "4.0.0") with { DefiningProjectFullPath = @"C:\elsewhere\Directory.Packages.props" }],
        };

        await Assert.That(Read(home, dump)).IsEmpty();
    }

    private static IReadOnlyList<DependencyPin> Read(TempHomeDirectory home, params PackagePinDump[] dumps)
        => new PackagePinReader(home.Provider, NullReporter.Instance).Read(dumps);

    private static PackagePinDump Dump(TempHomeDirectory home, params PackagePinDumpItem[] items)
        => new()
        {
            ProjectFullPath = home.GetFullPath(ProjectFileName),
            Items = [.. items.Select(item => item with { DefiningProjectFullPath = home.GetFullPath(ProjectFileName) })],
        };

    private static PackagePinDumpItem Item(
        string itemType,
        string id,
        string? version,
        string? versionOverride = null,
        string? updatePolicy = null,
        bool isImplicitlyDefined = false)
        => new()
        {
            ItemType = itemType,
            Id = id,
            Version = version,
            VersionOverride = versionOverride,
            UpdatePolicy = updatePolicy,
            IsImplicitlyDefined = isImplicitlyDefined,
            DefiningProjectFullPath = string.Empty,
        };

    private static string OneItemProject(string itemType, string id, string version)
        => $"""
            <Project>
              <ItemGroup>
                <{itemType} Include="{id}" Version="{version}" />
              </ItemGroup>
            </Project>
            """;

    private static void Write(TempHomeDirectory home, string relativePath, string content)
    {
        var path = home.GetFullPath(relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
