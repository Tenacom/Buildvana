// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.Testing;
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

        using var home = new TempHome();
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
        using var home = new TempHome();
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

        using var home = new TempHome();
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
        using var home = new TempHome();
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

        using var home = new TempHome();
        Write(home, ProjectFileName, project);
        var pins = Read(home, Dump(home, Item("PackageReference", "Serilog", "4.0.0")));
        await Assert.That(pins.Single().Management).IsEqualTo(PinManagement.IndirectVersion);
    }

    // MSBuild carries the layout of a Version child element into the value it evaluates, and the file states
    // that same layout: a literal version written this way is as managed as one written as an attribute.
    [Test]
    public async Task Read_OfAVersionStatedAsAChildElement_IsManaged()
    {
        const string project = """
                               <Project>
                                 <ItemGroup>
                                   <PackageVersion Include="Serilog">
                                     <Version>
                                       4.0.0
                                     </Version>
                                   </PackageVersion>
                                 </ItemGroup>
                               </Project>
                               """;

        using var home = new TempHome();
        Write(home, ProjectFileName, project);
        var pin = Read(home, Dump(home, Item("PackageVersion", "Serilog", "\n      4.0.0\n    "))).Single();
        await Assert.That(pin.Management).IsEqualTo(PinManagement.Managed);
        await Assert.That(pin.VersionText).IsEqualTo("4.0.0");
    }

    // The policy a pin states for itself reaches bv the same way, and the policy strings take no whitespace.
    [Test]
    public async Task Read_OfAPolicyStatedAsAChildElement_StatesThePolicyAlone()
    {
        using var home = new TempHome();
        Write(home, ProjectFileName, OneItemProject("PackageVersion", "Serilog", "4.0.0"));
        var item = Item("PackageVersion", "Serilog", "4.0.0", updatePolicy: "\n      patch-\n    ");
        await Assert.That(Read(home, Dump(home, item)).Single().MetadataPolicy).IsEqualTo("patch-");
    }

    // MSBuild has no absent metadatum, so an element a file leaves empty evaluates to its own layout. What
    // the file states there is nothing, and the policy of the pin is the one its scope or a pattern gives it.
    [Test]
    public async Task Read_OfAnEmptyPolicyElement_StatesNoPolicy()
    {
        using var home = new TempHome();
        Write(home, ProjectFileName, OneItemProject("PackageVersion", "Serilog", "4.0.0"));
        var item = Item("PackageVersion", "Serilog", "4.0.0", updatePolicy: "\n      \n    ");
        await Assert.That(Read(home, Dump(home, item)).Single().MetadataPolicy).IsNull();
    }

    // A version element a file leaves empty states no version, so the item is a reference to a pin declared
    // elsewhere, exactly as a PackageReference under central package management is.
    [Test]
    public async Task Read_OfAnEmptyVersionElement_IsNoPin()
    {
        using var home = new TempHome();
        Write(home, ProjectFileName, OneItemProject("PackageReference", "Serilog", "4.0.0"));
        await Assert.That(Read(home, Dump(home, Item("PackageReference", "Serilog", "\n      \n    ")))).IsEmpty();
    }

    // Each metadatum is judged on its own: an empty VersionOverride element overrides nothing, and the pin is
    // the managed one the version states.
    [Test]
    public async Task Read_OfAnEmptyVersionOverrideElement_IsNotAnOverride()
    {
        using var home = new TempHome();
        Write(home, ProjectFileName, OneItemProject("PackageReference", "Serilog", "4.0.0"));
        var item = Item("PackageReference", "Serilog", "4.0.0", versionOverride: "\n      \n    ");
        var pin = Read(home, Dump(home, item)).Single();
        await Assert.That(pin.Management).IsEqualTo(PinManagement.Managed);
        await Assert.That(pin.VersionText).IsEqualTo("4.0.0");
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

        using var home = new TempHome();
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

        using var home = new TempHome();
        Write(home, ProjectFileName, project);
        var item = Item("PackageReference", "Serilog", null, versionOverride: "4.1.0");
        var pin = Read(home, Dump(home, item)).Single();
        await Assert.That(pin.Management).IsEqualTo(PinManagement.VersionOverride);
        await Assert.That(pin.VersionText).IsEqualTo("4.1.0");
    }

    [Test]
    public async Task Read_OfAnUnmanagedVersionForm_KeepsTheFormsReason()
    {
        using var home = new TempHome();
        Write(home, ProjectFileName, OneItemProject("PackageVersion", "Serilog", "[4.0.0]"));
        var pins = Read(home, Dump(home, Item("PackageVersion", "Serilog", "[4.0.0]")));
        await Assert.That(pins.Single().Management).IsEqualTo(PinManagement.BracketExactVersion);
    }

    // A Directory.Packages.props above the repository, or one a package supplies, is not the repository's
    // to edit.
    [Test]
    public async Task Read_LeavesOutAnItemDeclaredOutsideTheRepository()
    {
        using var home = new TempHome();

        // Derived from the home directory, never written out: a literal Windows path is a relative path on
        // Linux, and would resolve to a file inside the repository.
        var outside = Path.GetFullPath(Path.Combine(home.RootPath, "..", "elsewhere", "Directory.Packages.props"));
        var dump = new PackagePinDump
        {
            ProjectFullPath = home.GetFullPath(ProjectFileName),
            Items = [Item("PackageVersion", "Serilog", "4.0.0") with { DefiningProjectFullPath = outside }],
        };

        await Assert.That(Read(home, dump)).IsEmpty();
    }

    private static IReadOnlyList<DependencyPin> Read(TempHome home, params PackagePinDump[] dumps)
        => new PackagePinReader(home.Provider, NullReporter.Instance).Read(dumps);

    private static PackagePinDump Dump(TempHome home, params PackagePinDumpItem[] items)
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

    private static void Write(TempHome home, string relativePath, string content)
    {
        var path = home.GetFullPath(relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
