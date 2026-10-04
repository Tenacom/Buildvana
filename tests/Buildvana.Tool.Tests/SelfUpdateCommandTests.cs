// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Json;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.SelfUpdate;
using Buildvana.Tool.Subcommands;
using NuGet.Versioning;
using Spectre.Console.Testing;

internal sealed class SelfUpdateCommandTests
{
    private const string OwnVersion = "2.1.41-preview";

    [Test]
    public async Task ExecuteAsync_PrintsTheSummary_AndReturnsZero()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, OwnVersion);
        WriteToolManifest(home, OwnVersion);
        WriteConfigFile(home, OwnVersion);
        const string project = """
            <Project>
              <ItemGroup>
                <PackageReference Include="Buildvana.Runtime" Version="2.1.41-preview" />
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("App.csproj", project);
        var console = new TestConsole();
        var command = new SelfUpdateCommand(CreateService(home), new SelfUpdateSettings(), console);

        var exitCode = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("bv: 2.1.41-preview (tool manifest, unchanged)");
        await Assert.That(console.Output).Contains("Buildvana.Sdk: 2.1.41-preview (global.json, unchanged)");
        await Assert.That(console.Output).Contains("Buildvana.Runtime: 2.1.41-preview (App.csproj, unchanged)");
        await Assert.That(console.Output).Contains("buildvana.jsonc: schema reference unchanged");
    }

    [Test]
    public async Task ExecuteAsync_WithoutConfigFile_OmitsTheConfigLine()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, OwnVersion);
        WriteToolManifest(home, OwnVersion);
        var console = new TestConsole();
        var command = new SelfUpdateCommand(CreateService(home), new SelfUpdateSettings(), console);

        _ = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(console.Lines.Count).IsEqualTo(2);
    }

    // With pins newer than the --to version, the update succeeds only when --force reaches the service — the
    // performed downgrade is the observable proof of the wiring.
    [Test]
    public async Task ExecuteAsync_ForwardsForceToTheUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        WriteToolManifest(home, "2.1.42-preview");
        var runner = new FakeProcessRunner();
        var settings = new SelfUpdateSettings { Force = true, To = OwnVersion };
        var command = new SelfUpdateCommand(CreateService(home, runner), settings, new TestConsole());

        var exitCode = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(runner.Runs.Count).IsEqualTo(1);
    }

    // The manifest pins a stable version, and the running bv is the one prerelease above it: a default run
    // would stay on the stable pin and hand off, and only --preview makes this bv the target.
    [Test]
    public async Task ExecuteAsync_ForwardsPreviewToTheUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.40");
        WriteToolManifest(home, "2.1.40");
        var console = new TestConsole();
        var versionSource = new FakePackageVersionSource().Knows("bv", ["2.1.40", OwnVersion]);
        var service = CreateService(home, new FakeProcessRunner(), versionSource);
        var command = new SelfUpdateCommand(service, new SelfUpdateSettings { Preview = true }, console);

        _ = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(console.Output).Contains("bv: 2.1.40 -> 2.1.41-preview (tool manifest)");
    }

    // The sources list 2.1.43-preview, so a default run would hand off to it: a repair stays on the manifest
    // pin, which is the running bv, and asks no source.
    [Test]
    public async Task ExecuteAsync_ForwardsRepairToTheUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.40-preview");
        WriteToolManifest(home, OwnVersion);
        var console = new TestConsole();
        var versionSource = new FakePackageVersionSource().Knows("bv", ["2.1.43-preview"]);
        var service = CreateService(home, new FakeProcessRunner(), versionSource);
        var command = new SelfUpdateCommand(service, new SelfUpdateSettings { Repair = true }, console);

        _ = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(versionSource.Asked.Count).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Buildvana.Sdk: 2.1.40-preview -> 2.1.41-preview (global.json)");
    }

    // The sources list 2.1.43-preview, so a default run would hand off to it and print nothing here. The
    // summary of an in-place update is the observable proof that --to reached the service.
    [Test]
    public async Task ExecuteAsync_ForwardsToVersionToTheUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.40-preview");
        WriteToolManifest(home, "2.1.40-preview");
        var console = new TestConsole();
        var versionSource = new FakePackageVersionSource().Knows("bv", ["2.1.40-preview", "2.1.43-preview"]);
        var settings = new SelfUpdateSettings { To = OwnVersion };
        var command = new SelfUpdateCommand(CreateService(home, new FakeProcessRunner(), versionSource), settings, console);

        var exitCode = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("bv: 2.1.40-preview -> 2.1.41-preview (tool manifest)");
        await Assert.That(console.Output).Contains("Buildvana.Sdk: 2.1.40-preview -> 2.1.41-preview (global.json)");
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidToVersion_FailsBeforeChangingAnything()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, OwnVersion);
        WriteToolManifest(home, OwnVersion);
        var runner = new FakeProcessRunner();
        var settings = new SelfUpdateSettings { To = "not-a-version" };
        var command = new SelfUpdateCommand(CreateService(home, runner), settings, new TestConsole());

        var exception = await Assert
            .That(async () => _ = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("--to");
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
    }

    // The summary belongs to the run that updated the files: a handed-off run prints nothing of its own, and
    // the exit code is the child's.
    [Test]
    public async Task ExecuteAsync_WhenHandedOff_PrintsNothing_AndReturnsTheChildExitCode()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, OwnVersion);
        WriteToolManifest(home, OwnVersion);
        var console = new TestConsole();
        var runner = new FakeProcessRunner { OnRunWithInheritedStdio = static (_, _) => 3 };
        var versionSource = new FakePackageVersionSource().Knows("bv", ["2.1.43-preview"]);
        var command = new SelfUpdateCommand(CreateService(home, runner, versionSource), new SelfUpdateSettings(), console);

        var exitCode = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(3);
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(1);
        await Assert.That(console.Output).IsEmpty();
    }

    private static SelfVersionService CreateService(
        TempHome home,
        FakeProcessRunner? processRunner = null,
        FakePackageVersionSource? versionSource = null)
    {
        var runner = processRunner ?? new FakeProcessRunner();
        return new SelfVersionService(
            NullReporter.Instance,
            home.Provider,
            new BuildvanaJsonConfigProvider(home.Provider),
            new JsonHelper(),
            runner,
            new FamilyPinUpdater(
                home.Provider,
                new Lazy<BuildvanaConfig>(static () => new BuildvanaConfig()),
                NullReporter.Instance),
            new SelfUpdateTargetResolver(
                versionSource ?? new FakePackageVersionSource().Knows("bv", [OwnVersion]),
                NuGetVersion.Parse(OwnVersion)),
            new SelfUpdateHandoff(NullReporter.Instance, home.Provider, runner, []),
            NuGetVersion.Parse(OwnVersion));
    }

    private static void WriteGlobalJson(TempHome home, string pin)
    {
        var content = $$"""
            {
              "msbuild-sdks": {
                "Buildvana.Sdk": "{{pin}}"
              }
            }

            """;
        home.WriteFile("global.json", content);
    }

    private static void WriteConfigFile(TempHome home, string version)
    {
        var content = $$"""
            {
              "$schema": "https://raw.githubusercontent.com/Tenacom/Buildvana/{{version}}/schemas/buildvana.schema.json"
            }

            """;
        home.WriteFile("buildvana.jsonc", content);
    }

    private static void WriteToolManifest(TempHome home, string version)
    {
        var content = $$"""
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "bv": {
                  "version": "{{version}}",
                  "commands": [
                    "bv"
                  ]
                }
              }
            }

            """;
        home.WriteFile("dotnet-tools.json", content);
    }
}
