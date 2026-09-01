// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;

internal sealed class SidecarReaderTests
{
    private const string CentralFileName = "Directory.TransitiveOverrides.props";
    private const string ProjectFileName = "src/Test/Test.TransitiveOverrides.props";

    private const string CentralContent = """
                                          <Project>
                                            <ItemGroup>
                                              <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
                                              <PackageVersion Include="Serilog" Version="4.0.0" />
                                            </ItemGroup>
                                          </Project>
                                          """;

    private const string ProjectContent = """
                                          <Project>
                                            <ItemGroup>
                                              <PackageReference Include="Newtonsoft.Json" PrivateAssets="all" />
                                              <PackageReference Include="Polly.Core" Version="8.7.0" PrivateAssets="all" />
                                            </ItemGroup>
                                          </Project>
                                          """;

    [Test]
    public async Task Read_StatesTheCentralVersions()
    {
        using var home = new TempHome();
        home.WriteFile(CentralFileName, CentralContent);
        var entries = Read(home);
        await Assert.That(entries.Select(static entry => entry.PackageId + " " + entry.Version))
            .IsEquivalentTo(["Newtonsoft.Json 13.0.3", "Serilog 4.0.0"]);
        await Assert.That(entries[0].DeclaringFile).IsEqualTo(CentralFileName);
    }

    // A promotion of a centrally pinned package carries no version of its own, and the report says which.
    [Test]
    public async Task Read_StatesAPromotionWithNoVersionOfItsOwn()
    {
        using var home = new TempHome();
        home.WriteFile(ProjectFileName, ProjectContent);
        var entries = Read(home);
        await Assert.That(entries[0].PackageId).IsEqualTo("Newtonsoft.Json");
        await Assert.That(entries[0].Version).IsNull();
        await Assert.That(entries[1].Version).IsEqualTo("8.7.0");
        await Assert.That(entries[1].DeclaringFile).IsEqualTo(ProjectFileName);
    }

    [Test]
    public async Task Read_WithNoOverrideFile_IsEmpty()
    {
        using var home = new TempHome();
        home.WriteFile("Directory.Packages.props", "<Project />");
        await Assert.That(Read(home)).IsEmpty();
    }

    // The next apply run rewrites the file whole, so one nothing can parse costs a warning, not the report.
    [Test]
    public async Task Read_WithAFileThatDoesNotParse_WarnsAndLeavesItOut()
    {
        using var home = new TempHome();
        home.WriteFile(CentralFileName, "<Project><ItemGroup>");
        var reporter = new CaptureReporter();
        var entries = new SidecarReader(home.Provider, reporter).Read();
        await Assert.That(entries).IsEmpty();
        await Assert.That(reporter.Messages.Any(static message => message.Level == MessageLevel.Warning)).IsTrue();
    }

    private static IReadOnlyList<TransitiveOverrideEntry> Read(TempHome home)
        => new SidecarReader(home.Provider, new CaptureReporter()).Read();
}
