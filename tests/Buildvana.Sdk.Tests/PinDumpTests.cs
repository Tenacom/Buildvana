// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class PinDumpTests
{
    [Test]
    public async Task Dump_StatesTheItemsAndTheEvaluationTheyComeFrom()
    {
        using var fixture = new PinDumpFixture();
        var dump = fixture.DumpPins(
            """
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="4.0.0" UpdatePolicy="patch-" />
                <GlobalPackageReference Include="Nerdbank.GitVersioning" Version="3.6.0" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            """).Single();

        await Assert.That(dump.ProjectFullPath).IsEqualTo(Path.Combine(fixture.ProjectDirectory, "Test.proj"));
        await Assert.That(dump.TargetFramework).IsNull();
        await Assert.That(dump.ManagePackageVersionsCentrally).IsTrue();
        var items = string.Join(",", dump.Items.Select(static i => i.ItemType + ":" + i.Id + ":" + i.Version));
        await Assert.That(items).IsEqualTo(
            "PackageVersion:Serilog:4.0.0,"
            + "GlobalPackageReference:Nerdbank.GitVersioning:3.6.0,"
            + "PackageReference:Newtonsoft.Json:13.0.3");
        await Assert.That(dump.Items[0].UpdatePolicy).IsEqualTo("patch-");
    }

    [Test]
    public async Task Dump_NamesTheFileThatDeclaresEachItem()
    {
        using var fixture = new PinDumpFixture();
        const string content = """
                               <Project>
                                 <ItemGroup>
                                   <PackageVersion Include="Serilog" Version="4.0.0" />
                                 </ItemGroup>
                               </Project>
                               """;
        var declaringFile = fixture.WriteFile("Packages.props", content);
        var dump = fixture.DumpPins("""  <Import Project="Packages.props" />""").Single();
        await Assert.That(dump.Items.Single().DefiningProjectFullPath).IsEqualTo(declaringFile);
    }

    [Test]
    public async Task Dump_OfAMultiTargetingProject_HasOneDumpPerTargetFramework()
    {
        using var fixture = new PinDumpFixture();
        var dumps = fixture.DumpPins(
            """
              <PropertyGroup>
                <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="4.0.0" />
                <PackageVersion Include="System.Text.Json" Version="10.0.0" Condition="'$(TargetFramework)' == 'net10.0'" />
              </ItemGroup>
            """);

        await Assert.That(dumps.Count).IsEqualTo(2);
        await Assert.That(dumps[0].TargetFramework).IsEqualTo("net10.0");
        await Assert.That(dumps[1].TargetFramework).IsEqualTo("net9.0");
        await Assert.That(dumps[0].Items.Count).IsEqualTo(2);
        await Assert.That(dumps[1].Items.Single().Id).IsEqualTo("Serilog");
    }

    [Test]
    public async Task Dump_StatesAnImplicitlyDefinedReferenceLikeAnyOther()
    {
        using var fixture = new PinDumpFixture();
        var dump = fixture.DumpPins(
            """
              <ItemGroup>
                <PackageReference Include="Serilog" Version="4.0.0" IsImplicitlyDefined="true" />
              </ItemGroup>
            """).Single();

        await Assert.That(dump.Items.Single().IsImplicitlyDefined).IsTrue();
    }
}
