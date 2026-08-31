// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Services.Solution;

// Discovery is where the readers meet, so these tests build a small repository on disk and let the real
// readers loose on it. Only MSBuild is faked: the process runner writes the pin dump its target would have
// written.
internal sealed class DependencyDiscoveryTests
{
    private const string GlobalJson = """
                                      {
                                        "sdk": { "version": "10.0.100" },
                                        "msbuild-sdks": { "Microsoft.Build.NoTargets": "3.7.0" }
                                      }
                                      """;

    private const string Project = """
                                   <Project>
                                     <ItemGroup>
                                       <PackageReference Include="Serilog" Version="4.0.0" />
                                     </ItemGroup>
                                   </Project>
                                   """;

    private static readonly DependencyScope[] AllScopes =
        [DependencyScope.NetSdk, DependencyScope.Sdks, DependencyScope.Tools, DependencyScope.Packages];

    [Test]
    public async Task DiscoverAsync_ReadsEverySelectedScope()
    {
        using var home = new TempHome();
        WriteRepository(home);
        var runner = new FakeProcessRunner { OnRun = WriteDump(home) };
        var inventory = await Discover(home, runner, AllScopes).ConfigureAwait(false);

        await Assert.That(inventory.NetSdk?.VersionText).IsEqualTo("10.0.100");
        await Assert.That(inventory.Tools.Single().Id).IsEqualTo("ngbv");

        // global.json states one project SDK, and the tool app's directive block states another.
        await Assert.That(inventory.Sdks.Select(static pin => pin.Id))
            .IsEquivalentTo(["Microsoft.Build.NoTargets", "Microsoft.Build.Traversal"]);

        // The solution's evaluation states one package, and the tool app's directive block another.
        await Assert.That(inventory.Packages.Select(static pin => pin.Id)).IsEquivalentTo(["Serilog", "Spectre.Console"]);
    }

    // A repository with no solution file still has a global.json, and reading it must not depend on one.
    [Test]
    public async Task DiscoverAsync_WithoutThePackagesScope_NeverReadsTheSolution()
    {
        using var home = new TempHome();
        WriteRepository(home);
        var runner = new FakeProcessRunner();
        var inventory = await Discover(home, runner, [DependencyScope.NetSdk], solution: null).ConfigureAwait(false);

        await Assert.That(inventory.NetSdk).IsNotNull();
        await Assert.That(inventory.Sdks).IsEmpty();
        await Assert.That(inventory.Tools).IsEmpty();
        await Assert.That(inventory.Packages).IsEmpty();
        await Assert.That(runner.Runs).IsEmpty();
    }

    private static async Task<DependencyInventory> Discover(
        TempHome home,
        IProcessRunner runner,
        IReadOnlyList<DependencyScope> scopes,
        string? solution = "Test.slnx")
    {
        var provider = home.Provider;
        var config = new BuildvanaConfig { FileBasedApps = ["/tools/"] };
        var jsonHelper = new JsonHelper();
        var reporter = NullReporter.Instance;
        var lazySolution = new Lazy<SolutionContext>(() => solution is null
            ? throw new InvalidOperationException("The solution must not be read.")
            : new HomeDirectorySolutionContextFactory(provider).Create());

        var discovery = new DependencyDiscovery(
            lazySolution,
            new GlobalJsonPinReader(provider, jsonHelper),
            new ToolPinReader(provider, jsonHelper),
            new DirectivePinReader(provider, config),
            new SolutionPinReader(provider, runner, reporter),
            new PackagePinReader(provider, reporter),
            new AdditionalGroupPinReader(provider, config, runner, reporter));

        return await discovery.DiscoverAsync(new HashSet<DependencyScope>(scopes)).ConfigureAwait(false);
    }

    private static void WriteRepository(TempHome home)
    {
        home.WriteFile("global.json", GlobalJson);
        home.WriteFile(".config/dotnet-tools.json", """{ "tools": { "ngbv": { "version": "0.5.1" } } }""");
        home.WriteFile("tools/report.cs", "#:package Spectre.Console@0.51.0\n#:sdk Microsoft.Build.Traversal@4.1.0\n");
        home.WriteFile("Test.slnx", """<Solution><Project Path="src/App/App.csproj" /></Solution>""");
        home.WriteFile("src/App/App.csproj", Project);
    }

    private static Func<string, IReadOnlyList<string>, ProcessResult> WriteDump(TempHome home)
        => (executable, args) =>
        {
            const string prefix = "-property:BV_PinDumpDirectory=";
            var directory = args.Single(arg => arg.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..];
            var projectPath = home.GetFullPath("src/App/App.csproj");
            var dump = new PackagePinDump
            {
                ProjectFullPath = projectPath,
                Items =
                [
                    new PackagePinDumpItem
                    {
                        ItemType = "PackageReference",
                        Id = "Serilog",
                        Version = "4.0.0",
                        DefiningProjectFullPath = projectPath,
                    },
                ],
            };

            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "app.json"),
                JsonSerializer.Serialize(dump, PackagePinDumpJsonContext.Default.PackagePinDump));
            return new ProcessResult(executable, 0, string.Empty, string.Empty, TimeSpan.Zero);
        };
}
