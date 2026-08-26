// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Json;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services;
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
        var console = new TestConsole();
        var command = new SelfUpdateCommand(CreateService(home), new SelfUpdateSettings(), console);

        var exitCode = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("bv: 2.1.41-preview (tool manifest, unchanged)");
        await Assert.That(console.Output).Contains("Buildvana.Sdk: 2.1.41-preview (global.json, unchanged)");
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

    // With pins newer than the running bv, the update succeeds only when --force reaches the service — the
    // performed downgrade is the observable proof of the wiring.
    [Test]
    public async Task ExecuteAsync_ForwardsForceToTheUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        WriteToolManifest(home, "2.1.42-preview");
        var runner = new FakeProcessRunner();
        var command = new SelfUpdateCommand(CreateService(home, runner), new SelfUpdateSettings { Force = true }, new TestConsole());

        var exitCode = await command.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(runner.Runs.Count).IsEqualTo(1);
    }

    private static SelfVersionService CreateService(TempHome home, FakeProcessRunner? processRunner = null)
        => new(
            NullReporter.Instance,
            home.Provider,
            new BuildvanaJsonConfigProvider(home.Provider),
            new JsonHelper(),
            processRunner ?? new FakeProcessRunner(),
            NuGetVersion.Parse(OwnVersion));

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
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, ".config"));
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
        home.WriteFile(Path.Combine(".config", "dotnet-tools.json"), content);
    }
}
