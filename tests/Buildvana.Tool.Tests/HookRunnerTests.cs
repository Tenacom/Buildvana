// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
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
        var (path, _, workingDirectory) = appRunner.Runs[0];
        await Assert.That(path).IsEqualTo(hookPath);
        await Assert.That(workingDirectory).IsEqualTo(home.RootPath);
    }

    [Test]
    public async Task RunHookAsync_PublishesCamelCaseContextFileThroughEnvironmentVariable()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        string? contextJson = null;
        appRunner.OnRun = (_, environment, _) => contextJson = File.ReadAllText(environment![HookRunner.ContextEnvironmentVariable]!);
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var context = SampleContext(home);

        _ = await runner.RunHookAsync("release", "post-release", context).ConfigureAwait(false);

        await Assert.That(contextJson).IsNotNull();
        using var document = JsonDocument.Parse(contextJson!);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("homeDirectory").GetString()).IsEqualTo(home.RootPath);
        await Assert.That(root.GetProperty("releaseVersion").GetString()).IsEqualTo("1.2.3");
        await Assert.That(root.GetProperty("releaseSemVer").GetString()).IsEqualTo("1.2.3-preview");
        await Assert.That(root.GetProperty("previousVersion").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(root.GetProperty("isPrerelease").GetBoolean()).IsTrue();
        await Assert.That(root.GetProperty("isPublicRelease").GetBoolean()).IsTrue();
        await Assert.That(root.GetProperty("artifactsDirectory").GetString()).IsEqualTo(context.ArtifactsDirectory);
        await Assert.That(root.GetProperty("producedPackages").GetProperty("Buildvana.Sdk").GetString()).IsEqualTo("1.2.3-preview");
        await Assert.That(root.GetProperty("dogfooded").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task RunHookAsync_CarriesPreviousVersion_WhenOneExists()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        string? contextJson = null;
        appRunner.OnRun = (_, environment, _) => contextJson = File.ReadAllText(environment![HookRunner.ContextEnvironmentVariable]!);
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var context = SampleContext(home) with { PreviousVersion = "1.2.2" };

        _ = await runner.RunHookAsync("release", "post-release", context).ConfigureAwait(false);

        using var document = JsonDocument.Parse(contextJson!);
        await Assert.That(document.RootElement.GetProperty("previousVersion").GetString()).IsEqualTo("1.2.2");
    }

    [Test]
    public async Task RunHookAsync_DeletesContextFile_AfterHookCompletes()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        string? contextPath = null;
        appRunner.OnRun = (_, environment, _) => contextPath = environment![HookRunner.ContextEnvironmentVariable];
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);

        _ = await runner.RunHookAsync("release", "post-release", SampleContext(home)).ConfigureAwait(false);

        await Assert.That(contextPath).IsNotNull();
        await Assert.That(File.Exists(contextPath!)).IsFalse();
    }

    [Test]
    public async Task RunHookAsync_WhenHookFails_PropagatesAndDeletesContextFile()
    {
        using var home = new TempHome();
        _ = WriteHookFile(home, "release", "post-release");
        var appRunner = new FakeFileBasedAppRunner();
        string? contextPath = null;
        appRunner.OnRun = (_, environment, _) =>
        {
            contextPath = environment![HookRunner.ContextEnvironmentVariable];
            throw new BuildFailedException("Hook failed.");
        };
        var runner = new HookRunner(NullReporter.Instance, home.Provider, appRunner);
        var context = SampleContext(home);

        await Assert.That(() => runner.RunHookAsync("release", "post-release", context)).Throws<BuildFailedException>();
        await Assert.That(contextPath).IsNotNull();
        await Assert.That(File.Exists(contextPath!)).IsFalse();
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

    private static PostReleaseHookContext SampleContext(TempHome home) => new()
    {
        HomeDirectory = home.RootPath,
        ReleaseVersion = "1.2.3",
        ReleaseSemVer = "1.2.3-preview",
        PreviousVersion = null,
        IsPrerelease = true,
        IsPublicRelease = true,
        ArtifactsDirectory = Path.Combine(home.RootPath, "artifacts", "Release"),
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
