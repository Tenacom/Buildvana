// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// What `bv dependencies prune` does when the scope that can hold an orphan is not selected. The rule lives
// in the command itself, where a refactor can undo it without any other test noticing.
internal sealed class DependenciesPruneCommandTests
{
    // Orphans exist among central package pins alone. With that scope left out there is nothing to look for,
    // nothing to restore, and no hook to run: `update` is the one command whose hook runs on an empty
    // selection.
    [Test]
    public async Task ExecuteAsync_WithoutThePackagesScope_SucceedsWithoutRunningAnything()
    {
        using var harness = new DependenciesCommandHarness();
        var exitCode = await harness.RunPruneAsync().ConfigureAwait(false);
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(harness.Steps).IsEmpty();
        await Assert.That(harness.SolutionWasAsked).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_OfACheckRunWithoutThePackagesScope_SucceedsAllTheSame()
    {
        using var harness = new DependenciesCommandHarness();
        var exitCode = await harness.RunPruneAsync("--check").ConfigureAwait(false);
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(harness.Steps).IsEmpty();
    }
}
