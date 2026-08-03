// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services;
using NuGet.Versioning;

internal sealed class SelfVersionServiceTests
{
    [Test]
    [Arguments("2.1.41-preview", "2.1.41-preview")]
    [Arguments("2.1.41-preview+g0123abc", "2.1.41-preview")]
    [Arguments("2.1.41-preview", "2.1.41-preview+g0123abc")]
    public async Task EnsureSdkVersionMatch_WithMatchingPin_Passes(string ownVersion, string pin)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, pin);
        var service = CreateService(home, ownVersion);

        await Assert.That(() => service.EnsureSdkVersionMatch()).ThrowsNothing();
    }

    [Test]
    [Arguments("2.1.40-preview")]
    [Arguments("2.1.42-preview")]
    [Arguments("2.1.41")]
    public async Task EnsureSdkVersionMatch_WithMismatchedPin_FailsNamingBothVersionsAndTheFix(string pin)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, pin);
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(() => service.EnsureSdkVersionMatch()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains(pin);
        await Assert.That(exception.Message).Contains("2.1.41-preview");
        await Assert.That(exception.Message).Contains("bv sync-sdk");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithoutGlobalJson_Fails()
    {
        using var home = new TempHome();
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(() => service.EnsureSdkVersionMatch()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("global.json");
        await Assert.That(exception.Message).Contains("bv sync-sdk");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithoutSdksSection_Fails()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", """{ "sdk": { "version": "10.0.302" } }""");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(() => service.EnsureSdkVersionMatch()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("msbuild-sdks");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithoutPinEntry_Fails()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", """{ "msbuild-sdks": { "Microsoft.Build.NoTargets": "3.7.134" } }""");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(() => service.EnsureSdkVersionMatch()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("Buildvana.Sdk");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithUnparseablePin_Fails()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "not-a-version");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(() => service.EnsureSdkVersionMatch()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("not-a-version");
    }

    [Test]
    public async Task SyncSdkAsync_WhenInSync_ChangesNothing()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        var before = home.ReadFile("global.json");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        await service.SyncSdkAsync().ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(before);
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("2.1.40-preview")]
    [Arguments("not-a-version")]
    public async Task SyncSdkAsync_WithOlderOrInvalidPin_RewritesPinInPlace(string pin)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, pin);
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        await service.SyncSdkAsync().ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(GlobalJsonText("2.1.41-preview"));
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SyncSdkAsync_WithoutPinEntry_AddsPin()
    {
        using var home = new TempHome();
        var content = """
            {
              "msbuild-sdks": {
                "Microsoft.Build.NoTargets": "3.7.134"
              }
            }

            """;
        home.WriteFile("global.json", content);
        var service = CreateService(home, "2.1.41-preview");

        await service.SyncSdkAsync().ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(
            """
            {
              "msbuild-sdks": {
                "Buildvana.Sdk": "2.1.41-preview",
                "Microsoft.Build.NoTargets": "3.7.134"
              }
            }

            """);
    }

    [Test]
    public async Task SyncSdkAsync_WithoutSdksSection_AddsSection()
    {
        using var home = new TempHome();
        var content = """
            {
              "sdk": {
                "version": "10.0.302"
              }
            }

            """;
        home.WriteFile("global.json", content);
        var service = CreateService(home, "2.1.41-preview");

        await service.SyncSdkAsync().ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(
            """
            {
              "msbuild-sdks": {
                "Buildvana.Sdk": "2.1.41-preview"
              },
              "sdk": {
                "version": "10.0.302"
              }
            }

            """);
    }

    [Test]
    public async Task SyncSdkAsync_WithoutGlobalJson_CreatesIt()
    {
        using var home = new TempHome();
        var service = CreateService(home, "2.1.41-preview");

        await service.SyncSdkAsync().ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo("{\n  \"msbuild-sdks\": {\n    \"Buildvana.Sdk\": \"2.1.41-preview\"\n  }\n}\n");
    }

    [Test]
    public async Task SyncSdkAsync_WithNewerPin_UpdatesToolViaDotnetToolUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        var before = home.ReadFile("global.json");
        WriteToolManifest(home, "2.1.41-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        await service.SyncSdkAsync().ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(before);
        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        var (executable, args, workingDirectory) = runner.Runs[0];
        await Assert.That(executable).IsNotNull();
        await Assert.That(args).IsEquivalentTo(["tool", "update", "bv", "--version", "2.1.42-preview"]);
        await Assert.That(workingDirectory).IsEqualTo(home.RootPath);
    }

    [Test]
    public async Task SyncSdkAsync_WhenToolUpdateFails_Propagates()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        WriteToolManifest(home, "2.1.41-preview");
        var runner = new FakeProcessRunner
        {
            OnRun = static (executable, args) => new ProcessResult($"{executable} {string.Join(' ', args)}", 1, string.Empty, "simulated failure", TimeSpan.Zero),
        };
        var service = CreateService(home, "2.1.41-preview", runner);

        await Assert.That(() => service.SyncSdkAsync()).Throws<BuildFailedException>();
    }

    [Test]
    public async Task SyncSdkAsync_WithNewerPinButNoManifest_Fails()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var exception = await Assert.That(() => service.SyncSdkAsync()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("dotnet-tools.json");
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SyncSdkAsync_WithNewerPinButForeignBv_Fails()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        WriteToolManifest(home, "2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var exception = await Assert.That(() => service.SyncSdkAsync()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("tool manifest");
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
    }

    private static SelfVersionService CreateService(TempHome home, string ownVersion, FakeProcessRunner? processRunner = null)
        => new(
            NullReporter.Instance,
            home.Provider,
            new JsonHelper(),
            processRunner ?? new FakeProcessRunner(),
            NuGetVersion.Parse(ownVersion));

    private static void WriteGlobalJson(TempHome home, string pin) => home.WriteFile("global.json", GlobalJsonText(pin));

    private static string GlobalJsonText(string pin) => $$"""
        {
          "sdk": {
            "version": "10.0.302"
          },
          "msbuild-sdks": {
            "Buildvana.Sdk": "{{pin}}",
            "Microsoft.Build.NoTargets": "3.7.134"
          }
        }

        """;

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
