// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;

// The order `bv dependencies update` writes in, and the verdict a check run returns. Both live in the
// command itself, where a refactor can undo them without any other test noticing.
internal sealed class DependenciesUpdateCommandTests
{
    // global.json goes last of everything, because a baseline naming an SDK that is not installed breaks
    // every later dotnet invocation. The tool update runs before the hook, and the hook before the baseline.
    [Test]
    public async Task ExecuteAsync_WritesTheBaselineLastOfAll()
    {
        using var harness = new DependenciesCommandHarness();
        var exitCode = await harness.RunAsync().ConfigureAwait(false);
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(harness.Steps.Count).IsEqualTo(2);
        await Assert.That(harness.Steps[0].Name).IsEqualTo("tool");
        await Assert.That(harness.Steps[1].Name).IsEqualTo("hook");
        await Assert.That(harness.ProcessRunner.Runs.Single().Args).Contains("0.6.0");
        await Assert.That(harness.GlobalJsonNow).Contains(DependenciesCommandHarness.NewNetSdkVersion);
    }

    // The hook is told the version the run foresees, and sees a global.json that still states the old one.
    // The project SDK it also sees is already moved: that scope is written before the hook runs.
    [Test]
    public async Task ExecuteAsync_RunsTheHookBeforeTheBaselineIsWritten()
    {
        using var harness = new DependenciesCommandHarness();
        _ = await harness.RunAsync().ConfigureAwait(false);
        var hook = harness.Steps.Single(step => step.Name == "hook");
        await Assert.That(hook.GlobalJson).Contains(DependenciesCommandHarness.OldNetSdkVersion);
        await Assert.That(hook.GlobalJson).DoesNotContain(DependenciesCommandHarness.NewNetSdkVersion);
        await Assert.That(hook.GlobalJson).Contains("1.1.0");
    }

    // A hook that would change something is pending work, exactly as a pin that has fallen behind is.
    [Test]
    public async Task ExecuteAsync_OfACheckRunWhoseHookFindsPendingWork_Fails()
    {
        using var harness = new DependenciesCommandHarness(hookExitCode: 1, upToDate: true);
        var exitCode = await harness.RunAsync("--check").ConfigureAwait(false);
        await Assert.That(exitCode).IsEqualTo(BuildFailedException.DefaultExitCode);
    }

    [Test]
    public async Task ExecuteAsync_OfACheckRunWithNothingPending_Succeeds()
    {
        using var harness = new DependenciesCommandHarness(upToDate: true);
        var exitCode = await harness.RunAsync("--check").ConfigureAwait(false);
        await Assert.That(exitCode).IsEqualTo(0);
    }

    // A check run resolves everything an apply run does, and writes none of it.
    [Test]
    public async Task ExecuteAsync_OfACheckRun_WritesNothing()
    {
        using var harness = new DependenciesCommandHarness();
        var exitCode = await harness.RunAsync("--check").ConfigureAwait(false);
        await Assert.That(exitCode).IsEqualTo(BuildFailedException.DefaultExitCode);
        await Assert.That(harness.GlobalJsonNow).Contains(DependenciesCommandHarness.OldNetSdkVersion);
        await Assert.That(harness.ProcessRunner.Runs).IsEmpty();
        await Assert.That(harness.Steps.Single().Name).IsEqualTo("hook");
    }

    // The packages scope alone spawns MSBuild, and these runs leave it out.
    [Test]
    public async Task ExecuteAsync_WithoutThePackagesScope_NeverReadsTheSolution()
    {
        using var harness = new DependenciesCommandHarness();
        _ = await harness.RunAsync().ConfigureAwait(false);
        await Assert.That(harness.SolutionWasAsked).IsFalse();
    }
}
