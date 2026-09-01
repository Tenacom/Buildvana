// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Services.Solution;

internal sealed class OverrideLifecycleTests
{
    private const string ProjectPath = "src/Test/Test.csproj";
    private const string AssetsPath = "src/Test/obj/project.assets.json";
    private const string CentralFileName = "Directory.TransitiveOverrides.props";
    private const string ProjectFileName = "src/Test/Test.TransitiveOverrides.props";
    private const string Vulnerable = "Newtonsoft.Json";

    [Test]
    public async Task RunAsync_WithNoFinding_WritesNoFileAndRestoresOnce()
    {
        using var home = NewHome(new AssetsFile().Resolves(Vulnerable, "13.0.3"));
        var restorer = new FakeDependencyRestorer();
        await RunAsync(home, restorer, new FakeVulnerabilityDataSource(), Versions()).ConfigureAwait(false);
        await Assert.That(File.Exists(home.GetFullPath(CentralFileName))).IsFalse();
        await Assert.That(restorer.Restores).IsEquivalentTo([true]);
    }

    // The central file states the version once, and the project's own file promotes the package to a
    // reference with no version of its own.
    [Test]
    public async Task RunAsync_WithAFinding_WritesTheVersionCentrallyAndPromotesIt()
    {
        using var home = NewHome(Finding("12.0.1"));
        var restorer = Lifting(home);
        await RunAsync(home, restorer, Advisories(), Versions()).ConfigureAwait(false);
        await Assert.That(home.ReadFile(CentralFileName)).Contains("""<PackageVersion Include="Newtonsoft.Json" Version="12.0.3" />""");
        await Assert.That(home.ReadFile(ProjectFileName)).Contains("""<PackageReference Include="Newtonsoft.Json" PrivateAssets="all" />""");
        await Assert.That(restorer.Restores).IsEquivalentTo([true, false]);
    }

    [Test]
    public async Task RunAsync_ForAProjectManagingItsOwnVersions_WritesTheVersionInTheProjectsFile()
    {
        using var home = NewHome(Finding("12.0.1"));
        await RunAsync(home, Lifting(home), Advisories(), Versions(), managesCentrally: false).ConfigureAwait(false);
        await Assert.That(File.Exists(home.GetFullPath(CentralFileName))).IsFalse();
        await Assert.That(home.ReadFile(ProjectFileName))
            .Contains("""<PackageReference Include="Newtonsoft.Json" Version="12.0.3" PrivateAssets="all" />""");
    }

    // A package the repository pins safely gets no version of its own: the project catches up with a decision
    // the repository already made.
    [Test]
    public async Task RunAsync_WithACentralPin_PromotesWithoutWritingAVersion()
    {
        using var home = NewHome(Finding("12.0.1"));
        await RunAsync(home, Lifting(home), Advisories(), Versions(), centralPin: "12.0.3").ConfigureAwait(false);
        await Assert.That(File.Exists(home.GetFullPath(CentralFileName))).IsFalse();
        await Assert.That(home.ReadFile(ProjectFileName)).Contains("""<PackageReference Include="Newtonsoft.Json" PrivateAssets="all" />""");
    }

    [Test]
    public async Task RunAsync_WithAFindingItCannotLift_WarnsAndWritesNothing()
    {
        using var home = NewHome(Finding("12.0.1"));
        var reporter = new CaptureReporter();
        var versions = new FakePackageVersionSource().Knows(Vulnerable, ["12.0.1"]);
        await RunAsync(home, new FakeDependencyRestorer(), Advisories(), versions, reporter: reporter).ConfigureAwait(false);
        await Assert.That(File.Exists(home.GetFullPath(CentralFileName))).IsFalse();
        var warnings = reporter.Messages.Where(static message => message.Level == MessageLevel.Warning).Select(static message => message.Message);
        await Assert.That(warnings.Any(static warning => warning.Contains("no override can lift it", StringComparison.Ordinal))).IsTrue();
    }

    // The second pass's graph no longer reports what the first one lifted. Writing only the latest findings
    // would drop that override and bring the vulnerability back.
    [Test]
    public async Task RunAsync_KeepsAnOverrideALaterPassNoLongerReports()
    {
        using var home = NewHome(Finding("12.0.1"));
        var restorer = new FakeDependencyRestorer();
        restorer.OnRestore = suppressed =>
        {
            if (suppressed)
            {
                return 0;
            }

            // The first active restore lifts the first package and turns up a second finding.
            home.WriteFile(
                AssetsPath,
                new AssetsFile()
                    .Resolves(Vulnerable, "12.0.3")
                    .Resolves("Serilog", "1.0.0")
                    .Reports("NU1902", "Serilog")
                    .ToString());

            restorer.OnRestore = _ => Settle(home);
            return 0;
        };

        var advisories = Advisories().Knows("Serilog", "(, 1.0.0]");
        var versions = Versions().Knows("Serilog", ["1.0.0", "1.1.0"]);
        await RunAsync(home, restorer, advisories, versions).ConfigureAwait(false);
        var central = home.ReadFile(CentralFileName);
        await Assert.That(central).Contains("""<PackageVersion Include="Newtonsoft.Json" Version="12.0.3" />""");
        await Assert.That(central).Contains("""<PackageVersion Include="Serilog" Version="1.1.0" />""");
    }

    [Test]
    public async Task RunAsync_WithIncompleteVulnerabilityData_ReportsAFailedStep()
    {
        using var home = NewHome(new AssetsFile().Resolves(Vulnerable, "12.0.1").Reports("NU1900"));
        var exception = await Assert.That(async () => await RunAsync(home, new FakeDependencyRestorer(), Advisories(), Versions())
            .ConfigureAwait(false)).Throws<BuildFailedException>();

        await Assert.That(exception!.ExitCode).IsEqualTo(3);
        await Assert.That(exception.Message).Contains("vulnerability data");
    }

    [Test]
    public async Task RunAsync_WithARestoreThatFailedForAnotherReason_ReportsAFailedStep()
    {
        using var home = NewHome(new AssetsFile().Resolves(Vulnerable, "12.0.1").Reports("NU1101", "Contoso.Widgets", "Error"));
        var exception = await Assert.That(async () => await RunAsync(home, new FakeDependencyRestorer(), Advisories(), Versions())
            .ConfigureAwait(false)).Throws<BuildFailedException>();

        await Assert.That(exception!.ExitCode).IsEqualTo(3);
        await Assert.That(exception.Message).Contains("NU1101");
    }

    // An audit source with no data at all is NuGet's own warning, and it says nothing about bv's own step.
    [Test]
    public async Task RunAsync_WithAnAuditSourceThatHasNoData_PassesTheWarningOn()
    {
        using var home = NewHome(new AssetsFile().Resolves(Vulnerable, "13.0.3").Reports("NU1905"));
        var reporter = new CaptureReporter();
        await RunAsync(home, new FakeDependencyRestorer(), new FakeVulnerabilityDataSource(), Versions(), reporter: reporter)
            .ConfigureAwait(false);

        var warnings = reporter.Messages.Where(static message => message.Level == MessageLevel.Warning);
        await Assert.That(warnings.Any(static warning => warning.Message.Contains("NU1905", StringComparison.Ordinal))).IsTrue();
    }

    // A graph that reports a new package at every pass is a graph that never settles, and the bound on the
    // passes is what turns that into a failed step instead of a command that never returns.
    [Test]
    public async Task RunAsync_WhenTheGraphNeverSettles_ReportsAFailedStep()
    {
        using var home = NewHome(NeverSettling(1));
        var pass = 1;
        var restorer = new FakeDependencyRestorer();
        restorer.OnRestore = suppressed =>
        {
            if (suppressed)
            {
                return 0;
            }

            pass++;
            home.WriteFile(AssetsPath, NeverSettling(pass).ToString());
            return 0;
        };

        var advisories = new FakeVulnerabilityDataSource();
        var versions = new FakePackageVersionSource();
        for (var index = 1; index <= 12; index++)
        {
            _ = advisories.Knows($"Contoso.Package{index}", "(, 1.0.0]");
            _ = versions.Knows($"Contoso.Package{index}", ["1.0.0", "1.1.0"]);
        }

        var exception = await Assert.That(async () => await RunAsync(home, restorer, advisories, versions).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.ExitCode).IsEqualTo(1);
        await Assert.That(exception.Message).Contains("did not settle");
    }

    private static AssetsFile Finding(string version)
        => new AssetsFile().Resolves(Vulnerable, version).Reports("NU1902", Vulnerable);

    private static AssetsFile NeverSettling(int pass)
        => new AssetsFile().Resolves($"Contoso.Package{pass}", "1.0.0").Reports("NU1902", $"Contoso.Package{pass}");

    private static FakeVulnerabilityDataSource Advisories() => new FakeVulnerabilityDataSource().Knows(Vulnerable, "(, 12.0.2]");

    private static FakePackageVersionSource Versions()
        => new FakePackageVersionSource().Knows(Vulnerable, ["12.0.1", "12.0.2", "12.0.3"]);

    private static TempHome NewHome(AssetsFile assets)
    {
        var home = new TempHome();
        home.WriteFile(ProjectPath, "<Project />");
        home.WriteFile(AssetsPath, assets.ToString());
        return home;
    }

    // What a restore that applied the override would leave behind: the package resolved at the version the
    // override states, and no finding about it. The suppressed restore leaves the graph alone, because that
    // graph is the one the test wrote, and it is what the lifecycle reads first.
    private static FakeDependencyRestorer Lifting(TempHome home)
    {
        var restorer = new FakeDependencyRestorer();
        restorer.OnRestore = suppressed => suppressed ? 0 : Settle(home);
        return restorer;
    }

    private static int Settle(TempHome home)
    {
        home.WriteFile(AssetsPath, new AssetsFile().Resolves(Vulnerable, "12.0.3").ToString());
        return 0;
    }

    private static Task RunAsync(
        TempHome home,
        FakeDependencyRestorer restorer,
        FakeVulnerabilityDataSource advisories,
        FakePackageVersionSource versions,
        bool managesCentrally = true,
        string? centralPin = null,
        CaptureReporter? reporter = null)
    {
        var actualReporter = reporter ?? new CaptureReporter();

        // The restorer is the only thing that would use the solution, and it is faked.
        var solution = new Lazy<SolutionContext>(static () => null!);
        var lifecycle = new OverrideLifecycle(
            solution,
            home.Provider,
            restorer,
            advisories,
            versions,
            new EffectivePolicyResolver(new DependenciesConfig()),
            new SidecarWriter(home.Provider, actualReporter),
            actualReporter);

        return lifecycle.RunAsync([Evaluation(home, managesCentrally, centralPin)]);
    }

    private static PackagePinDump Evaluation(TempHome home, bool managesCentrally, string? centralPin)
    {
        var pin = new PackagePinDumpItem
        {
            ItemType = "PackageVersion",
            Id = Vulnerable,
            Version = centralPin,
            DefiningProjectFullPath = home.GetFullPath("Directory.Packages.props"),
        };

        return new PackagePinDump
        {
            ProjectFullPath = home.GetFullPath(ProjectPath),
            ProjectAssetsFile = home.GetFullPath(AssetsPath),
            TargetFramework = "net10.0",
            ManagePackageVersionsCentrally = managesCentrally,
            NuGetAuditLevel = "low",
            Items = centralPin is null ? [] : [pin],
        };
    }
}
