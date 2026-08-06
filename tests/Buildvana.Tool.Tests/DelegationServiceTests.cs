// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Tool.Infrastructure.Delegation;
using NuGet.Versioning;

internal sealed class DelegationServiceTests
{
    private const string OwnVersion = "2.1.41-preview";

    [Test]
    public async Task TryDelegate_WithDelegationMarker_RunsInPlace()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(runner);

        var result = await service.TryDelegateAsync(Context(home, markerPresent: true)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryDelegate_WithSkipDelegation_RunsInPlace()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(runner);

        var result = await service.TryDelegateAsync(Context(home, skipDelegation: true)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("update")]
    [Arguments("UPDATE")]
    public async Task TryDelegate_WithUpdateSubcommand_RunsInPlace(string subcommand)
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(runner);

        var result = await service.TryDelegateAsync(Context(home, subcommand: subcommand)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryDelegate_WithoutToolManifest_RunsInPlace()
    {
        using var home = new TempHome();
        MarkAsHome(home);
        var runner = new FakeProcessRunner();
        var service = CreateService(runner);

        var result = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryDelegate_WithoutBvManifestEntry_RunsInPlace()
    {
        using var home = new TempHome();
        MarkAsHome(home);
        WriteToolManifest(home, """{ "version": 1, "isRoot": true, "tools": {} }""");
        var runner = new FakeProcessRunner();
        var service = CreateService(runner);

        var result = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryDelegate_WithUnreadableManifest_RunsInPlaceAndWarns()
    {
        using var home = new TempHome();
        MarkAsHome(home);
        WriteToolManifest(home, "this is not JSON");
        var runner = new FakeProcessRunner();
        var output = new StringWriter();
        var service = CreateService(runner, output);

        var result = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
        await Assert.That(output.ToString()).Contains("delegation skipped");
    }

    [Test]
    public async Task TryDelegate_WithMatchingPin_FromPackageCache_RunsInPlace()
    {
        using var home = CreateDelegatableHome(OwnVersion);
        var runner = new FakeProcessRunner();
        var service = CreateService(runner);

        var result = await service.TryDelegateAsync(Context(home, layout: InstallLayout.PackageCache)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
    }

    // The manifest's install always runs: a version-matching bv still delegates when it does not run from
    // the package cache (a global install, or an unrecognized layout) — silently, since naming a version
    // would only repeat the one that is already running.
    [Test]
    [Arguments(InstallLayout.ToolStore)]
    [Arguments(InstallLayout.Unknown)]
    public async Task TryDelegate_WithMatchingPin_FromNonLocalLayout_DelegatesSilently(InstallLayout layout)
    {
        using var home = CreateDelegatableHome(OwnVersion);
        var runner = new FakeProcessRunner();
        var output = new StringWriter();
        var service = CreateService(runner, output);

        var result = await service.TryDelegateAsync(Context(home, layout: layout)).ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(1);
        await Assert.That(output.ToString()).IsEmpty();
    }

    [Test]
    [Arguments("2.1.40-preview", InstallLayout.PackageCache)]
    [Arguments("2.1.42-preview", InstallLayout.ToolStore)]
    [Arguments("2.1.42-preview", InstallLayout.Unknown)]
    public async Task TryDelegate_WithMismatchedPin_DelegatesAndPrintsInfoLine(string pin, InstallLayout layout)
    {
        using var home = CreateDelegatableHome(pin);
        var runner = new FakeProcessRunner();
        var output = new StringWriter();
        var service = CreateService(runner, output);

        var result = await service.TryDelegateAsync(Context(home, layout: layout)).ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(1);
        await Assert.That(output.ToString()).Contains($"Delegating to bv {pin}");
    }

    [Test]
    public async Task TryDelegate_RestoresThenRunsPinnedBvWithOriginalArguments()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner { OnRunWithInheritedStdio = static (_, _) => 42 };
        var service = CreateService(runner);
        string[] rawArgs = ["build", "--verbosity", "detailed", "--", "/p:Answer=42"];

        var result = await service.TryDelegateAsync(Context(home, rawArgs: rawArgs)).ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(42);

        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        var (restoreExecutable, restoreArgs, restoreDirectory) = runner.Runs[0];
        await Assert.That(restoreExecutable).IsNotNull();
        await Assert.That(restoreArgs).IsEquivalentTo(["tool", "restore"]);

        // Home-directory discovery reports the home path with a trailing separator, per its contract.
        await Assert.That(Path.TrimEndingDirectorySeparator(restoreDirectory!)).IsEqualTo(home.RootPath);

        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(1);
        var run = runner.InheritedStdioRuns[0];

        // Restore and the delegated run must go through the same dotnet muxer.
        await Assert.That(run.Executable).IsEqualTo(restoreExecutable);
        await Assert.That(run.Args).IsEquivalentTo(["tool", "run", "bv", "--", "build", "--verbosity", "detailed", "--", "/p:Answer=42"]);
        await Assert.That(Path.TrimEndingDirectorySeparator(run.WorkingDirectory!)).IsEqualTo(home.RootPath);
        await Assert.That(run.Environment).IsNotNull();
        await Assert.That(run.Environment!["BV_DELEGATED"]).IsEqualTo(OwnVersion);
    }

    [Test]
    public async Task TryDelegate_WhenRestoreFails_FailsWithoutRunning()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner
        {
            OnRun = static (executable, args) => new ProcessResult($"{executable} {string.Join(' ', args)}", 1, string.Empty, "simulated failure", TimeSpan.Zero),
        };
        var service = CreateService(runner);
        var context = Context(home);

        var exception = await Assert.That(() => service.TryDelegateAsync(context)).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("tool restore");
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
    }

    private static DelegationService CreateService(FakeProcessRunner runner, StringWriter? output = null)
        => new(new JsonHelper(), runner, NuGetVersion.Parse(OwnVersion), output ?? new StringWriter());

    private static DelegationContext Context(
        TempHome home,
        string[]? rawArgs = null,
        string? subcommand = "build",
        bool skipDelegation = false,
        bool markerPresent = false,
        InstallLayout layout = InstallLayout.ToolStore)
        => new(
            rawArgs ?? ["build"],
            subcommand,
            skipDelegation,
            markerPresent,
            layout,
            home.RootPath);

    // Plants a .git marker so that home-directory discovery, which the delegation decision runs from the
    // start directory, stops exactly at the temporary home.
    private static void MarkAsHome(TempHome home)
    {
        var gitDirectory = Directory.CreateDirectory(Path.Combine(home.RootPath, ".git"));
        File.WriteAllText(Path.Combine(gitDirectory.FullName, "HEAD"), "ref: refs/heads/main\n");
    }

    private static TempHome CreateDelegatableHome(string pinnedVersion)
    {
        var home = new TempHome();
        MarkAsHome(home);
        var manifest = $$"""
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "bv": {
                  "version": "{{pinnedVersion}}",
                  "commands": [
                    "bv"
                  ]
                }
              }
            }

            """;
        WriteToolManifest(home, manifest);
        return home;
    }

    private static void WriteToolManifest(TempHome home, string content)
    {
        _ = Directory.CreateDirectory(Path.Combine(home.RootPath, ".config"));
        home.WriteFile(Path.Combine(".config", "dotnet-tools.json"), content);
    }
}
