// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
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
    [Arguments("self-update")]
    [Arguments("SELF-UPDATE")]
    public async Task TryDelegate_WithSelfUpdateSubcommand_RunsInPlace(string subcommand)
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

    // A bv entry the dotnet CLI itself could not use (see ToolManifest.ReadBvPin) reads as "no pin", but
    // deserves a warning — the repository clearly meant to pin something.
    [Test]
    [Arguments("""{ "version": 1, "isRoot": true, "tools": { "bv": { "version": "not-a-version", "commands": [ "bv" ] } } }""", "not-a-version")]
    [Arguments("""{ "version": 1, "isRoot": true, "tools": { "bv": { "commands": [ "bv" ] } } }""", "has no version")]
    public async Task TryDelegate_WithUnusableManifestEntry_RunsInPlaceAndWarns(string manifestContent, string expectedDetail)
    {
        using var home = new TempHome();
        MarkAsHome(home);
        WriteToolManifest(home, manifestContent);
        var runner = new FakeProcessRunner();
        var output = new StringWriter();
        var service = CreateService(runner, output);

        var result = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(result).IsNull();
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(0);
        await Assert.That(output.ToString()).Contains("delegation skipped");
        await Assert.That(output.ToString()).Contains(expectedDetail);
    }

    // The dotnet CLI matches manifest keys case-insensitively (it lowercases them into package IDs), so a
    // differently-cased bv entry still pins bv and must delegate like any other.
    [Test]
    public async Task TryDelegate_WithDifferentlyCasedManifestKey_ReadsThePin()
    {
        using var home = new TempHome();
        MarkAsHome(home);
        WriteToolManifest(home, """{ "version": 1, "isRoot": true, "tools": { "BV": { "version": "2.1.40-preview", "commands": [ "bv" ] } } }""");
        var runner = new FakeProcessRunner();
        var output = new StringWriter();
        var service = CreateService(runner, output);

        var result = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(1);
        await Assert.That(output.ToString()).Contains("Delegating to bv 2.1.40-preview");
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

    // The pinned bv is already installed: `dotnet tool run` needs no restore, so none must run — the common
    // delegated invocation costs a single process spawn.
    [Test]
    public async Task TryDelegate_WithPinnedVersionInstalled_SkipsRestore()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(runner, probe: CreateHitProbe(home, "2.1.40-preview"));

        var result = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(runner.Runs.Count).IsEqualTo(0);
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(1);
    }

    // A cache hit for a different version is not a hit for the pin: the restore must still run.
    [Test]
    public async Task TryDelegate_WithDifferentVersionInstalled_RestoresFirst()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner();
        var service = CreateService(runner, probe: CreateHitProbe(home, "2.1.39-preview"));

        _ = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(runner.Runs.Count).IsEqualTo(1);
        await Assert.That(runner.Runs[0].Args).IsEquivalentTo(["tool", "restore"]);
    }

    // The restore's streamed output reaches the delegation writer, so a cold download is visible instead of
    // looking like a hang.
    [Test]
    public async Task TryDelegate_StreamsRestoreOutput()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner
        {
            OnRun = static (executable, args) => new ProcessResult($"{executable} {string.Join(' ', args)}", 0, "Restored bv.\nRestored ngbv.", string.Empty, TimeSpan.Zero),
        };
        var output = new StringWriter();
        var service = CreateService(runner, output);

        _ = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(output.ToString()).Contains("Restored bv.");
        await Assert.That(output.ToString()).Contains("Restored ngbv.");
    }

    // A failed restore must not block the run: `dotnet tool restore` attempts every manifest tool even when one
    // fails, so bv may well be available although an unrelated tool's feed is not — and when bv itself is the
    // missing one, `dotnet tool run` fails on its own with an actionable message.
    [Test]
    public async Task TryDelegate_WhenRestoreFails_WarnsAndRunsAnyway()
    {
        using var home = CreateDelegatableHome("2.1.40-preview");
        var runner = new FakeProcessRunner
        {
            OnRun = static (executable, args) => new ProcessResult($"{executable} {string.Join(' ', args)}", 1, string.Empty, "simulated failure", TimeSpan.Zero),
            OnRunWithInheritedStdio = static (_, _) => 42,
        };
        var output = new StringWriter();
        var service = CreateService(runner, output);

        var result = await service.TryDelegateAsync(Context(home)).ConfigureAwait(false);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(runner.InheritedStdioRuns.Count).IsEqualTo(1);
        await Assert.That(output.ToString()).Contains("Warning: 'dotnet tool restore' failed");

        // The restore's stderr was streamed before the failure was judged, so the cause is on screen too.
        await Assert.That(output.ToString()).Contains("simulated failure");
    }

    // The default probe watches a directory that never exists, so it always misses and the restore path is
    // exercised; tests that want the fast path build a real cache with CreateHitProbe.
    private static DelegationService CreateService(
        FakeProcessRunner runner,
        StringWriter? output = null,
        ToolResolverCacheProbe? probe = null)
        => new(
            new JsonHelper(),
            runner,
            probe ?? new ToolResolverCacheProbe(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())),
            NuGetVersion.Parse(OwnVersion),
            output ?? new StringWriter());

    // Builds a resolver cache whose bv entry matches the given version and points at an existing file, so the
    // probe reports the version as already installed.
    private static ToolResolverCacheProbe CreateHitProbe(TempHome home, string version)
    {
        var executable = Path.Combine(home.RootPath, "fake-bv.dll");
        File.WriteAllText(executable, string.Empty);
        var cacheDirectory = Path.Combine(home.RootPath, "toolResolverCache");
        _ = Directory.CreateDirectory(Path.Combine(cacheDirectory, "1"));
        var content = $$"""[{ "Version": "{{version}}", "PathToExecutable": {{JsonSerializer.Serialize(executable)}} }]""";
        File.WriteAllText(Path.Combine(cacheDirectory, "1", "bv"), content);
        return new ToolResolverCacheProbe(cacheDirectory);
    }

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
