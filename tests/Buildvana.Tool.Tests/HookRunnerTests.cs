// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Hooks;

internal sealed class HookRunnerTests
{
    [Test]
    public async Task RunHookAsync_WithoutHookFile_SkipsAndReturnsFalse()
    {
        using var home = new TempHome();
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        var ran = await runner.RunHookAsync("release", "post-release", SampleContext(home)).ConfigureAwait(false);

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

        var ran = await runner.RunHookAsync("release", "post-release", SampleContext(home)).ConfigureAwait(false);

        await Assert.That(ran).IsTrue();
        await Assert.That(appRunner.Runs.Count).IsEqualTo(1);
        var (path, environment, workingDirectory) = appRunner.Runs[0];
        await Assert.That(path).IsEqualTo(hookPath);
        await Assert.That(environment).IsNull();
        await Assert.That(workingDirectory).IsEqualTo(home.RootPath);
    }

    [Test]
    public async Task RunHookAsync_WritesCamelCaseContextFileAtWellKnownPath()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var contextPath = ContextPath(home);
        string? contextJson = null;
        appRunner.OnRun = (_, _, _) => contextJson = File.ReadAllText(contextPath);
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var context = SampleContext(home);

        _ = await runner.RunHookAsync("release", "post-release", context).ConfigureAwait(false);

        await Assert.That(contextJson).IsNotNull();
        using var document = JsonDocument.Parse(contextJson!);
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        await Assert.That(paths.GetProperty("homeDirectory").GetString()).IsEqualTo(home.RootPath);
        await Assert.That(paths.GetProperty("artifactsDirectory").GetString()).IsEqualTo(context.Paths.ArtifactsDirectory);
        await Assert.That(paths.GetProperty("scratchDirectory").GetString()).IsEqualTo(context.Paths.ScratchDirectory);
        var release = root.GetProperty("release");
        await Assert.That(release.GetProperty("version").GetString()).IsEqualTo("1.2.3");
        await Assert.That(release.GetProperty("semVer").GetString()).IsEqualTo("1.2.3-preview");
        await Assert.That(release.GetProperty("previousVersion").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(release.GetProperty("isPrerelease").GetBoolean()).IsTrue();
        await Assert.That(release.GetProperty("isPublicRelease").GetBoolean()).IsTrue();
        await Assert.That(root.GetProperty("producedPackages").GetProperty("Buildvana.Sdk").GetString()).IsEqualTo("1.2.3-preview");
        await Assert.That(root.GetProperty("dogfooded").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task RunHookAsync_CarriesPreviousVersion_WhenOneExists()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var context = SampleContext(home);
        context = context with { Release = context.Release with { PreviousVersion = "1.2.2" } };

        _ = await runner.RunHookAsync("release", "post-release", context).ConfigureAwait(false);

        var contextJson = await File.ReadAllTextAsync(ContextPath(home)).ConfigureAwait(false);
        using var document = JsonDocument.Parse(contextJson);
        await Assert.That(document.RootElement.GetProperty("release").GetProperty("previousVersion").GetString()).IsEqualTo("1.2.2");
    }

    [Test]
    public async Task RunHookAsync_LeavesContextFileInPlace_AfterHookCompletes()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        _ = await runner.RunHookAsync("release", "post-release", SampleContext(home)).ConfigureAwait(false);

        await Assert.That(File.Exists(ContextPath(home))).IsTrue();
    }

    [Test]
    public async Task RunHookAsync_WhenHookFails_PropagatesAndLeavesContextFile()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner
        {
            OnRun = (_, _, _) => throw new BuildFailedException("Hook failed."),
        };
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var context = SampleContext(home);

        await Assert.That(() => runner.RunHookAsync("release", "post-release", context)).Throws<BuildFailedException>();
        await Assert.That(File.Exists(ContextPath(home))).IsTrue();
    }

    [Test]
    public async Task CleanBuildCachesAsync_WithoutHooksDirectory_CleansNothing()
    {
        using var home = new TempHome();
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        await runner.CleanBuildCachesAsync().ConfigureAwait(false);

        await Assert.That(appRunner.CleanedPaths.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CleanBuildCachesAsync_CleansEveryHookFileRecursively()
    {
        using var home = new TempHome();
        var firstHookPath = WriteHookFile(home, "release", "post-release");
        var secondHookPath = WriteHookFile(home, "pack", "pre-pack");
        var appRunner = new FakeFileBasedAppRunner();
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        await runner.CleanBuildCachesAsync().ConfigureAwait(false);

        await Assert.That(appRunner.CleanedPaths.Count).IsEqualTo(2);
        await Assert.That(appRunner.CleanedPaths.Contains(firstHookPath)).IsTrue();
        await Assert.That(appRunner.CleanedPaths.Contains(secondHookPath)).IsTrue();
    }

    private static string ContextPath(TempHome home)
        => Path.GetFullPath(WellKnownPaths.GetHookContextFile("release", "post-release"), home.RootPath);

    private static PostReleaseHookContext SampleContext(TempHome home) => new()
    {
        Paths = new()
        {
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
        Dogfooded = false,
    };

    private static string WriteHookFile(TempHome home, string command, string moment)
    {
        var directory = Path.Combine(home.RootPath, ".buildvana", "hooks", command);
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, moment + ".cs");
        File.WriteAllText(path, "// test hook, never executed by these tests");
        return path;
    }
}
