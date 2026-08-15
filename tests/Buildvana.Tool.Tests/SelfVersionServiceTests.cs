// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
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

        await Assert.That(service.EnsureSdkVersionMatch).ThrowsNothing();
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

        var exception = await Assert.That(service.EnsureSdkVersionMatch).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains(pin);
        await Assert.That(exception.Message).Contains("2.1.41-preview");
        await Assert.That(exception.Message).Contains("bv update");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithoutGlobalJson_Fails()
    {
        using var home = new TempHome();
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(service.EnsureSdkVersionMatch).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("global.json");
        await Assert.That(exception.Message).Contains("bv update");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithoutSdksSection_Fails()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", """{ "sdk": { "version": "10.0.302" } }""");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(service.EnsureSdkVersionMatch).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("msbuild-sdks");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithoutPinEntry_Fails()
    {
        using var home = new TempHome();
        home.WriteFile("global.json", """{ "msbuild-sdks": { "Microsoft.Build.NoTargets": "3.7.134" } }""");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(service.EnsureSdkVersionMatch).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("Buildvana.Sdk");
    }

    [Test]
    public async Task EnsureSdkVersionMatch_WithUnparseablePin_Fails()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "not-a-version");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert.That(service.EnsureSdkVersionMatch).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("not-a-version");
    }

    [Test]
    [Arguments("2.1.41-preview", "2.1.41-preview")]
    [Arguments("2.1.41-preview+g0123abc", "2.1.41-preview")]
    [Arguments("2.1.41-preview", "2.1.41-preview+g0123abc")]
    public async Task UpdateRepository_WhenEverythingCurrent_ChangesNothing(string ownVersion, string pin)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, pin);
        WriteToolManifest(home, pin);
        var before = home.ReadFile("global.json");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, ownVersion, runner);

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(before);
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
        await Assert.That(summary.ToolManifestLine).Contains("tool manifest, unchanged");
        await Assert.That(summary.GlobalJsonLine).Contains("global.json, unchanged");
        await Assert.That(summary.ConfigFileLine).IsNull();
    }

    [Test]
    public async Task UpdateRepository_WithOlderManifestPin_RunsDotnetToolUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        var (executable, args, workingDirectory) = runner.Runs[0];
        await Assert.That(executable).IsNotNull();
        await Assert.That(args).IsEquivalentTo(["tool", "update", "bv", "--version", "2.1.41-preview"]);
        await Assert.That(workingDirectory).IsEqualTo(home.RootPath);
        await Assert.That(summary.ToolManifestLine).IsEqualTo("bv: 2.1.40-preview -> 2.1.41-preview (tool manifest)");
    }

    [Test]
    public async Task UpdateRepository_WithoutManifestEntry_RunsDotnetToolInstall()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        var (_, args, _) = runner.Runs[0];
        await Assert.That(args).IsEquivalentTo(["tool", "install", "bv", "--version", "2.1.41-preview", "--create-manifest-if-needed"]);
        await Assert.That(summary.ToolManifestLine).IsEqualTo("bv: 2.1.41-preview (tool manifest, added)");
    }

    // The dotnet CLI matches manifest keys case-insensitively (it lowercases them into package IDs), so a
    // differently-cased bv entry is still an existing entry: the pin must go through `dotnet tool update`,
    // not `dotnet tool install`.
    [Test]
    public async Task UpdateRepository_WithDifferentlyCasedManifestKey_RunsDotnetToolUpdate()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        const string manifest
            = """{ "version": 1, "isRoot": true, "tools": { "BV": { "version": "2.1.40-preview", "commands": [ "bv" ] } } }""";
        WriteRawToolManifest(home, manifest);
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        await Assert.That(runner.Runs[0].Args).IsEquivalentTo(["tool", "update", "bv", "--version", "2.1.41-preview"]);
        await Assert.That(summary.ToolManifestLine).IsEqualTo("bv: 2.1.40-preview -> 2.1.41-preview (tool manifest)");
    }

    [Test]
    [Arguments("2.1.40-preview")]
    [Arguments("not-a-version")]
    public async Task UpdateRepository_WithOlderOrInvalidSdkPin_RewritesPinInPlace(string pin)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, pin);
        WriteToolManifest(home, "2.1.41-preview");
        var service = CreateService(home, "2.1.41-preview");

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(GlobalJsonText("2.1.41-preview"));
        await Assert.That(summary.GlobalJsonLine).IsEqualTo($"Buildvana.Sdk: {pin} -> 2.1.41-preview (global.json)");
    }

    [Test]
    public async Task UpdateRepository_WithoutPinEntry_AddsPin()
    {
        using var home = new TempHome();
        const string content = """
            {
              "msbuild-sdks": {
                "Microsoft.Build.NoTargets": "3.7.134"
              }
            }

            """;
        home.WriteFile("global.json", content);
        WriteToolManifest(home, "2.1.41-preview");
        var service = CreateService(home, "2.1.41-preview");

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo(
            """
            {
              "msbuild-sdks": {
                "Buildvana.Sdk": "2.1.41-preview",
                "Microsoft.Build.NoTargets": "3.7.134"
              }
            }

            """);
        await Assert.That(summary.GlobalJsonLine).IsEqualTo("Buildvana.Sdk: 2.1.41-preview (global.json, added)");
    }

    [Test]
    public async Task UpdateRepository_WithoutSdksSection_AddsSection()
    {
        using var home = new TempHome();
        const string content = """
            {
              "sdk": {
                "version": "10.0.302"
              }
            }

            """;
        home.WriteFile("global.json", content);
        WriteToolManifest(home, "2.1.41-preview");
        var service = CreateService(home, "2.1.41-preview");

        _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

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
    public async Task UpdateRepository_WithoutGlobalJson_CreatesIt()
    {
        using var home = new TempHome();
        WriteToolManifest(home, "2.1.41-preview");
        var service = CreateService(home, "2.1.41-preview");

        _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile("global.json")).IsEqualTo("{\n  \"msbuild-sdks\": {\n    \"Buildvana.Sdk\": \"2.1.41-preview\"\n  }\n}\n");
    }

    // A directory named global.json is invisible to File.Exists, so the update takes the create-new-file path
    // and the write hits a directory — the denied-access failure mode (not an IOException) on every platform.
    [Test]
    public async Task UpdateRepository_WithDeniedGlobalJsonWrite_Fails()
    {
        using var home = new TempHome();
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, "global.json"));
        WriteToolManifest(home, "2.1.41-preview");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert
            .That(async () => _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("Could not write to");
        await Assert.That(exception.InnerException).IsTypeOf<UnauthorizedAccessException>();
    }

    [Test]
    [Arguments("2.1.42-preview", "2.1.41-preview", "tool manifest")]
    [Arguments("2.1.41-preview", "2.1.42-preview", "global.json")]
    [Arguments("2.1.42-preview", "2.1.42-preview", "tool manifest")]
    public async Task UpdateRepository_WithNewerPin_FailsWithoutForce(string manifestPin, string sdkPin, string offender)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, sdkPin);
        WriteToolManifest(home, manifestPin);
        var before = home.ReadFile("global.json");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var exception = await Assert
            .That(async () => _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("downgrade");
        await Assert.That(exception.Message).Contains("--force");
        await Assert.That(exception.Message).Contains(offender);
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
        await Assert.That(home.ReadFile("global.json")).IsEqualTo(before);
    }

    [Test]
    public async Task UpdateRepository_WithNewerPins_AndForce_Downgrades()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        WriteToolManifest(home, "2.1.42-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var summary = await service.UpdateRepositoryAsync(force: true).ConfigureAwait(false);

        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        var (_, args, _) = runner.Runs[0];
        await Assert.That(args).IsEquivalentTo(["tool", "update", "bv", "--version", "2.1.41-preview", "--allow-downgrade"]);
        await Assert.That(home.ReadFile("global.json")).IsEqualTo(GlobalJsonText("2.1.41-preview"));
        await Assert.That(summary.ToolManifestLine).IsEqualTo("bv: 2.1.42-preview -> 2.1.41-preview (tool manifest)");
    }

    // --allow-downgrade keys on the actual direction of the manifest change, not on --force: a forced update
    // whose manifest pin is older is a plain upgrade, and the CLI's own downgrade guard must stay armed for it.
    [Test]
    public async Task UpdateRepository_WithForce_AndOlderManifestPin_UpdatesWithoutAllowDowngrade()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.42-preview");
        WriteToolManifest(home, "2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var summary = await service.UpdateRepositoryAsync(force: true).ConfigureAwait(false);

        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        await Assert.That(runner.Runs[0].Args).IsEquivalentTo(["tool", "update", "bv", "--version", "2.1.41-preview"]);
        await Assert.That(summary.ToolManifestLine).IsEqualTo("bv: 2.1.40-preview -> 2.1.41-preview (tool manifest)");
    }

    // An entry whose version the dotnet CLI cannot parse is beyond any `dotnet tool` verb's reach — update must
    // fail up front with a message pointing at the file, before touching anything.
    [Test]
    [Arguments("""{ "version": 1, "isRoot": true, "tools": { "bv": { "version": "not-a-version", "commands": [ "bv" ] } } }""", "not-a-version")]
    [Arguments("""{ "version": 1, "isRoot": true, "tools": { "bv": { "commands": [ "bv" ] } } }""", "has no version")]
    public async Task UpdateRepository_WithUnusableManifestEntry_FailsBeforeChangingAnything(string manifestContent, string expectedDetail)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.40-preview");
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, ".config"));
        home.WriteFile(Path.Combine(".config", "dotnet-tools.json"), manifestContent);
        var before = home.ReadFile("global.json");
        var runner = new FakeProcessRunner();
        var service = CreateService(home, "2.1.41-preview", runner);

        var exception = await Assert
            .That(async () => _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains(expectedDetail);
        await Assert.That(exception.Message).Contains(".config/dotnet-tools.json");
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
        await Assert.That(home.ReadFile("global.json")).IsEqualTo(before);
    }

    [Test]
    public async Task UpdateRepository_WhenToolUpdateFails_Propagates()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.40-preview");
        var runner = new FakeProcessRunner
        {
            OnRun = static (executable, args) => new ProcessResult($"{executable} {string.Join(' ', args)}", 1, string.Empty, "simulated failure", TimeSpan.Zero),
        };
        var service = CreateService(home, "2.1.41-preview", runner);

        await Assert
            .That(async () => _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    [Arguments("buildvana.jsonc")]
    [Arguments("buildvana.json")]
    public async Task UpdateRepository_WithRecognizedSchemaReference_RewritesVersionSegment(string configFileName)
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        home.WriteFile(configFileName, SchemaConfigText("2.1.40-preview"));
        var service = CreateService(home, "2.1.41-preview");

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile(configFileName)).IsEqualTo(SchemaConfigText("2.1.41-preview"));
        await Assert.That(summary.ConfigFileLine).IsEqualTo($"{configFileName}: schema reference updated");
    }

    // The rewrite must be byte-preserving outside the version segment; comments are the acid test, since a
    // parse-and-save cycle would lose them.
    [Test]
    public async Task UpdateRepository_WithSchemaReference_PreservesCommentsAndFormatting()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        const string content = """
            // This comment must survive the update.
            {
              /* so must this one */
              "$schema": "https://raw.githubusercontent.com/Tenacom/Buildvana/2.1.40-preview/schemas/buildvana.schema.json"
            }

            """;
        home.WriteFile("buildvana.jsonc", content);
        var service = CreateService(home, "2.1.41-preview");

        _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile("buildvana.jsonc")).IsEqualTo(content.Replace("2.1.40-preview", "2.1.41-preview", StringComparison.Ordinal));
    }

    [Test]
    public async Task UpdateRepository_WithCurrentSchemaReference_LeavesFileUntouched()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        home.WriteFile("buildvana.jsonc", SchemaConfigText("2.1.41-preview"));
        var before = home.ReadFile("buildvana.jsonc");
        var service = CreateService(home, "2.1.41-preview");

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile("buildvana.jsonc")).IsEqualTo(before);
        await Assert.That(summary.ConfigFileLine).IsEqualTo("buildvana.jsonc: schema reference unchanged");
    }

    [Test]
    public async Task UpdateRepository_WithForeignSchemaReference_LeavesItAlone()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        const string content = """
            {
              "$schema": "https://example.com/hand-rolled.schema.json"
            }

            """;
        home.WriteFile("buildvana.jsonc", content);
        var service = CreateService(home, "2.1.41-preview");

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(home.ReadFile("buildvana.jsonc")).IsEqualTo(content);
        await Assert.That(summary.ConfigFileLine).IsEqualTo("buildvana.jsonc: schema reference not recognized, left unchanged");
    }

    [Test]
    public async Task UpdateRepository_WithoutSchemaReference_ReportsIt()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        home.WriteFile("buildvana.jsonc", "{}\n");
        var service = CreateService(home, "2.1.41-preview");

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(summary.ConfigFileLine).IsEqualTo("buildvana.jsonc: no schema reference found");
    }

    [Test]
    public async Task UpdateRepository_WithConfigInvalidUnderOwnModel_WarnsButSucceeds()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        home.WriteFile("buildvana.jsonc", """{ "definitelyNotASetting": true }""" + "\n");
        var reporter = new CaptureReporter();
        var service = CreateService(home, "2.1.41-preview", reporter: reporter);

        var summary = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(summary.ConfigFileLine).IsEqualTo("buildvana.jsonc: no schema reference found");
        var warnings = WarningsOf(reporter);
        await Assert.That(warnings.Count).IsEqualTo(1);
        await Assert.That(warnings[0]).Contains("buildvana.jsonc");
    }

    [Test]
    public async Task UpdateRepository_WithValidConfig_DoesNotWarn()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        home.WriteFile("buildvana.jsonc", SchemaConfigText("2.1.41-preview"));
        var reporter = new CaptureReporter();
        var service = CreateService(home, "2.1.41-preview", reporter: reporter);

        _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false);

        await Assert.That(WarningsOf(reporter).Count).IsEqualTo(0);
    }

    [Test]
    public async Task UpdateRepository_WithMultipleConfigFiles_Fails()
    {
        using var home = new TempHome();
        WriteGlobalJson(home, "2.1.41-preview");
        WriteToolManifest(home, "2.1.41-preview");
        home.WriteFile("buildvana.json", "{}\n");
        home.WriteFile("buildvana.jsonc", "{}\n");
        var service = CreateService(home, "2.1.41-preview");

        var exception = await Assert
            .That(async () => _ = await service.UpdateRepositoryAsync(force: false).ConfigureAwait(false))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("Multiple");
    }

    private static SelfVersionService CreateService(
        TempHome home,
        string ownVersion,
        FakeProcessRunner? processRunner = null,
        IReporter? reporter = null)
        => new(
            reporter ?? NullReporter.Instance,
            home.Provider,
            new BuildvanaJsonConfigProvider(home.Provider),
            new JsonHelper(),
            processRunner ?? new FakeProcessRunner(),
            NuGetVersion.Parse(ownVersion));

    private static List<string> WarningsOf(CaptureReporter reporter) => MessagesOf(reporter, MessageLevel.Warning);

    private static List<string> MessagesOf(CaptureReporter reporter, MessageLevel level)
        => [.. reporter.Messages.Where(m => m.Level == level).Select(static m => m.Message)];

    private static void WriteGlobalJson(TempHome home, string pin) => home.WriteFile("global.json", GlobalJsonText(pin));

    private static string SchemaConfigText(string version) => $$"""
        {
          "$schema": "https://raw.githubusercontent.com/Tenacom/Buildvana/{{version}}/schemas/buildvana.schema.json"
        }

        """;

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
        WriteRawToolManifest(home, content);
    }

    private static void WriteRawToolManifest(TempHome home, string content)
    {
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, ".config"));
        home.WriteFile(Path.Combine(".config", "dotnet-tools.json"), content);
    }
}
