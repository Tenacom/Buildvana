// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

internal sealed class SidecarWriterTests
{
    private const string CentralFileName = "Directory.TransitiveOverrides.props";
    private const string ProjectFileName = "src/Test/Test.TransitiveOverrides.props";
    private const string ProjectPath = "src/Test/Test.csproj";

    [Test]
    public async Task Write_StatesTheCentralVersionsOfThePackagesTheRepositoryDoesNotPin()
    {
        using var home = new TempHome();
        Write(home, Plan([Entry("Serilog", "4.0.0"), Entry("Newtonsoft.Json", "13.0.3")], []));
        var content = home.ReadFile(CentralFileName);
        await Assert.That(content).Contains("""<PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />""");
        await Assert.That(content).Contains("""<PackageVersion Include="Serilog" Version="4.0.0" />""");
        await Assert.That(content).Contains("Do not edit");
    }

    // Two runs finding the same vulnerabilities must write the same bytes, whatever order the graph was read in.
    [Test]
    public async Task Write_OrdersEntriesById()
    {
        using var home = new TempHome();
        Write(home, Plan([Entry("Serilog", "4.0.0"), Entry("Newtonsoft.Json", "13.0.3")], []));
        var content = home.ReadFile(CentralFileName);
        await Assert.That(content.IndexOf("Newtonsoft.Json", StringComparison.Ordinal))
            .IsLessThan(content.IndexOf("Serilog", StringComparison.Ordinal));
    }

    [Test]
    public async Task Write_PromotesAPackageWithNoVersionWhereTheCentralFileStatesOne()
    {
        using var home = new TempHome();
        Write(home, Plan([Entry("Serilog", "4.0.0")], [Project(home, [new PackageOverride("Serilog", null)])]));
        await Assert.That(home.ReadFile(ProjectFileName)).Contains("""<PackageReference Include="Serilog" PrivateAssets="all" />""");
    }

    [Test]
    public async Task Write_PromotesAPackageWithAVersionWhereNoCentralFileStatesOne()
    {
        using var home = new TempHome();
        Write(home, Plan([], [Project(home, [Entry("Serilog", "4.0.0")])]));
        await Assert.That(home.ReadFile(ProjectFileName))
            .Contains("""<PackageReference Include="Serilog" Version="4.0.0" PrivateAssets="all" />""");
    }

    [Test]
    public async Task Write_AProjectWithNothingToPromote_HasItsFileRemoved()
    {
        using var home = new TempHome();
        home.WriteFile(ProjectFileName, "<Project />");
        Write(home, Plan([], [Project(home, [])]));
        await Assert.That(File.Exists(home.GetFullPath(ProjectFileName))).IsFalse();
    }

    [Test]
    public async Task Write_WithNothingToStateCentrally_RemovesTheCentralFile()
    {
        using var home = new TempHome();
        home.WriteFile(CentralFileName, "<Project />");
        Write(home, Plan([], []));
        await Assert.That(File.Exists(home.GetFullPath(CentralFileName))).IsFalse();
    }

    // A rewritten file invalidates the restore that follows it, so a run that changes nothing must not touch
    // the files it would have written.
    [Test]
    public async Task Write_AFileAlreadyStatingWhatTheRunWould_LeavesItAlone()
    {
        using var home = new TempHome();
        var plan = Plan([Entry("Serilog", "4.0.0")], []);
        Write(home, plan);
        var path = home.GetFullPath(CentralFileName);
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);
        Write(home, plan);
        await Assert.That(File.GetLastWriteTimeUtc(path)).IsEqualTo(stamp);
    }

    private static void Write(TempHome home, TransitiveOverridePlan plan)
        => new SidecarWriter(home.Provider, new CaptureReporter()).Write(plan);

    private static TransitiveOverridePlan Plan(PackageOverride[] central, ProjectOverrides[] projects)
        => new() { Central = central, Projects = projects };

    // The project itself is written so that its directory exists, as it does wherever bv runs for real.
    private static ProjectOverrides Project(TempHome home, PackageOverride[] promotions)
    {
        home.WriteFile(ProjectPath, "<Project />");
        return new() { ProjectFullPath = home.GetFullPath(ProjectPath), Promotions = promotions };
    }

    private static PackageOverride Entry(string packageId, string version) => new(packageId, NuGetVersion.Parse(version));
}
