// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.IO;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Hooks;
using Buildvana.Tool.Utilities;

internal sealed class HookRunnerTests
{
    [Test]
    public async Task RunHookAsync_WithoutHookFile_SkipsAndReturnsFalse()
    {
        using var home = new TempHome();
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        var ran = await runner.RunHookAsync(SampleArgs(home)).ConfigureAwait(false);

        await Assert.That(ran).IsFalse();
        await Assert.That(appRunner.Runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RunHookAsync_WithHookFile_RunsHookFromHomeDirectory()
    {
        using var home = new TempHome();
        var hookPath = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        var ran = await runner.RunHookAsync(SampleArgs(home)).ConfigureAwait(false);

        await Assert.That(ran).IsTrue();
        await Assert.That(appRunner.Runs.Count).IsEqualTo(1);
        var (path, environment, workingDirectory) = appRunner.Runs[0];
        await Assert.That(path).IsEqualTo(hookPath);
        await Assert.That(environment).IsNull();
        await Assert.That(workingDirectory).IsEqualTo(home.RootPath);
    }

    [Test]
    public async Task RunHookAsync_WritesCamelCaseArgsFileAtWellKnownPath()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var argsPath = ArgsPath(home);
        string? argsJson = null;
        appRunner.OnRun = (_, _, _) => argsJson = File.ReadAllText(argsPath);
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var args = SampleArgs(home);

        _ = await runner.RunHookAsync(args).ConfigureAwait(false);

        await Assert.That(argsJson).IsNotNull();
        using var document = JsonDocument.Parse(argsJson!);
        var root = document.RootElement;
        var runtimeInfo = root.GetProperty("runtimeInfo");
        await Assert.That(runtimeInfo.GetProperty("version").GetString()).IsEqualTo("1.2.3-preview");
        await Assert.That(runtimeInfo.GetProperty("delegatingVersion").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(runtimeInfo.GetProperty("homeDirectory").GetString()).IsEqualTo(home.RootPath);
        await Assert.That(runtimeInfo.GetProperty("artifactsDirectory").GetString()).IsEqualTo(args.RuntimeInfo.ArtifactsDirectory);
        await Assert.That(runtimeInfo.GetProperty("scratchDirectory").GetString()).IsEqualTo(args.RuntimeInfo.ScratchDirectory);
        var release = root.GetProperty("release");
        await Assert.That(release.GetProperty("version").GetString()).IsEqualTo("1.2.3");
        await Assert.That(release.GetProperty("semVer").GetString()).IsEqualTo("1.2.3-preview");
        await Assert.That(release.GetProperty("previousVersion").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(release.GetProperty("isPrerelease").GetBoolean()).IsTrue();
        await Assert.That(release.GetProperty("isPublicRelease").GetBoolean()).IsTrue();
        await Assert.That(root.GetProperty("producedPackages").GetProperty("Buildvana.Sdk").GetString()).IsEqualTo("1.2.3-preview");
        await Assert.That(root.GetProperty("dogfooding").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task RunHookAsync_CarriesPreviousVersion_WhenOneExists()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var args = SampleArgs(home);
        args = args with { Release = args.Release with { PreviousVersion = "1.2.2" } };

        _ = await runner.RunHookAsync(args).ConfigureAwait(false);

        var argsJson = await File.ReadAllTextAsync(ArgsPath(home)).ConfigureAwait(false);
        using var document = JsonDocument.Parse(argsJson);
        await Assert.That(document.RootElement.GetProperty("release").GetProperty("previousVersion").GetString()).IsEqualTo("1.2.2");
    }

    [Test]
    public async Task RunHookAsync_LeavesArgsFileInPlace_AfterHookCompletes()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        _ = await runner.RunHookAsync(SampleArgs(home)).ConfigureAwait(false);

        await Assert.That(File.Exists(ArgsPath(home))).IsTrue();
    }

    [Test]
    public async Task RunHookAsync_WhenHookFails_PropagatesAndLeavesArgsFile()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner
        {
            OnRun = (_, _, _) => throw new BuildFailedException("Hook failed."),
        };
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var args = SampleArgs(home);

        await Assert.That(() => runner.RunHookAsync(args)).Throws<BuildFailedException>();
        await Assert.That(File.Exists(ArgsPath(home))).IsTrue();
    }

    [Test]
    public async Task RunHookAsync_WhenArgsFileCannotBeWritten_FailsWithCleanError()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        // A directory planted at the args file's well-known path denies writing the args file.
        _ = Directory.CreateDirectory(ArgsPath(home));
        var args = SampleArgs(home);

        Task<bool> Act() => runner.RunHookAsync(args);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not write to");
        await Assert.That(appRunner.Runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CleanBuildCaches_WithoutHooksDirectory_DeletesNothing()
    {
        using var home = new TempHome();
        var wouldBeHookPath = Path.Combine(home.RootPath, ".buildvana", "hooks", "release", "post-release.cs");
        var artifactsPath = CreateFakeArtifactsDirectory(wouldBeHookPath);
        var runner = new HookRunner(NullReporter.Instance, home.Provider, new FakeFileBasedAppRunner());

        try
        {
            runner.CleanBuildCaches();

            await Assert.That(Directory.Exists(artifactsPath)).IsTrue();
        }
        finally
        {
            Directory.Delete(artifactsPath, recursive: true);
        }
    }

    [Test]
    public async Task CleanBuildCaches_DeletesArtifactsOfEveryHookFileRecursively()
    {
        using var home = new TempHome();
        var firstArtifactsPath = CreateFakeArtifactsDirectory(WriteHookFile(home, "release", "post-release"));
        var secondArtifactsPath = CreateFakeArtifactsDirectory(WriteHookFile(home, "pack", "pre-pack"));
        var runner = new HookRunner(NullReporter.Instance, home.Provider, new FakeFileBasedAppRunner());

        try
        {
            runner.CleanBuildCaches();

            await Assert.That(Directory.Exists(firstArtifactsPath)).IsFalse();
            await Assert.That(Directory.Exists(secondArtifactsPath)).IsFalse();
        }
        finally
        {
            UserDirectory.DeleteIfExists(firstArtifactsPath);
            UserDirectory.DeleteIfExists(secondArtifactsPath);
        }
    }

    private static string ArgsPath(TempHome home)
        => Path.GetFullPath(WellKnownPaths.GetHookArgsFile("release", "post-release"), home.RootPath);

    private static PostReleaseHookArgs SampleArgs(TempHome home) => new()
    {
        RuntimeInfo = new()
        {
            Version = "1.2.3-preview",
            DelegatingVersion = null,
            HomeDirectory = home.RootPath,
            ArtifactsDirectory = Path.Combine(home.RootPath, "artifacts", "Release"),
            ScratchDirectory = Path.Combine(home.RootPath, WellKnownPaths.ScratchDirectory),
        },
        Release = new()
        {
            Version = "1.2.3",
            SemVer = "1.2.3-preview",
            PreviousVersion = null,
            IsPrerelease = true,
            IsPublicRelease = true,
        },
        ProducedPackages = new Dictionary<string, string> { ["Buildvana.Sdk"] = "1.2.3-preview" },
        Dogfooding = false,
    };

    private static string WriteHookFile(TempHome home, string context, string @event)
    {
        var directory = Path.Combine(home.RootPath, ".buildvana", "hooks", context);
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, @event + ".cs");
        File.WriteAllText(path, "// test hook, never executed by these tests");
        return path;
    }

    // The hook file paths these tests use are unique per run (TempHome lives in a randomly-named temporary
    // directory), so the computed artifacts directories cannot collide with those of real file-based apps.
    private static string CreateFakeArtifactsDirectory(string hookPath)
    {
        var path = FileBasedAppHelper.GetArtifactsDirectory(hookPath)
            ?? throw new InvalidOperationException("This machine has no file-based app cache root.");
        _ = Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "build-success.cache"), string.Empty);
        return path;
    }
}
