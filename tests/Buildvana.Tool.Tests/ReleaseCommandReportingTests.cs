// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

/// <summary>
/// End-to-end tests of what a successful <c>release</c> records at notice level: the lines a run at the
/// default verbosity shows, which are the whole account of the release a user gets.
/// </summary>
/// <remarks>
/// <para>Counted lines are asserted one repository shape at a time, singular and plural alike. Which shape
/// produces which wording is invisible from the code — the branches sit in the middle of a command that only
/// runs end to end — and a count that reads wrong is exactly the kind of thing that rots unnoticed.</para>
/// <para>The harness anchors the process's current directory and sets an environment variable, so these tests
/// cannot share the process with others that run at the same time.</para>
/// </remarks>
[NotInParallel]
internal sealed class ReleaseCommandReportingTests
{
    // Asking for a prerelease line that is already a prerelease line changes nothing, which is a decision
    // worth recording: the release goes on, and the version file it leaves behind is the one it found.
    [Test]
    public async Task Release_WithBumpToUnstable_OnAPrereleaseLine_RecordsTheSpecUnchanged()
    {
        using var harness = new ReleaseHarness(new() { Dogfood = false });

        var exitCode = await harness.RunAsync("--bump", "unstable").ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(harness.Notices).Contains("Version spec not changed.");
        await Assert.That(harness.ReadFile("VERSION").Trim()).IsEqualTo("2.3-");
    }

    // Public API files are shipped in pairs - the unshipped file is emptied into the shipped one - so the
    // count is always even, and the singular case the other counted lines have does not exist here.
    [Test]
    public async Task Release_ShippingPublicApis_CountsBothFilesOfThePair()
    {
        using var harness = new ReleaseHarness(new()
        {
            VersionSpec = "2.3",
            Dogfood = false,
            CheckPublicApi = false,
            UnshippedPublicApi = "#nullable enable\nTest.Thing\n",
        });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.Notices).Contains("2 public API files were modified.");
    }

    [Test]
    public async Task Release_WithoutPublicApis_RecordsThatNoneWereModified()
    {
        using var harness = new ReleaseHarness(new() { VersionSpec = "2.3", Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.Notices).Contains("No public API files were modified.");
    }

    // A hook that changes nothing is not a hook that did not run, and the two have to look different:
    // the release still commits and publishes, and the line is what says the hook had its turn.
    [Test]
    public async Task Release_WithHookThatChangesNothing_RecordsNoModifiedFiles()
    {
        using var harness = new ReleaseHarness(new() { WithHook = true, Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.AppRunner.Runs.Count).IsEqualTo(1);
        await Assert.That(harness.Notices).Contains("The post-release hook modified no files.");
    }

    [Test]
    public async Task Release_WithHookThatChangesOneFile_RecordsItInTheSingular()
    {
        using var harness = new ReleaseHarness(new() { WithHook = true, Dogfood = false });

        // ReSharper disable once AccessToDisposedClosure // False positive: the hook runs before the harness is disposed
        harness.HookBehavior = () => harness.WriteFile("docs/release-notes.md", "Released.\n");

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.Notices).Contains("The post-release hook modified 1 file.");
    }

    [Test]
    public async Task Release_WithHookThatChangesSeveralFiles_RecordsTheirNumber()
    {
        using var harness = new ReleaseHarness(new() { WithHook = true, Dogfood = false });

        // ReSharper disable once AccessToDisposedClosure // False positive: the hook runs before the harness is disposed
        harness.HookBehavior = () =>
        {
            harness.WriteFile("docs/release-notes.md", "Released.\n");
            harness.WriteFile("docs/announcement.md", "Announcing the release.\n");
        };

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.Notices).Contains("The post-release hook modified 2 files.");
    }

    // Dogfooding that rewrites nothing is the failure mode the self-reference update is most likely to have
    // - a name that matches no reference rewrites just as silently as a repository that has none - so the
    // line has to be there even when the answer is zero.
    [Test]
    public async Task Release_WithNoSelfReferenceTargets_RecordsThatNoneWereModified()
    {
        using var harness = new ReleaseHarness(new() { SelfReferenceTargets = 0 });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.Notices).Contains("No self-referenced files were modified.");
    }

    [Test]
    public async Task Release_WithOneSelfReferenceTarget_RecordsItInTheSingular()
    {
        using var harness = new ReleaseHarness(new() { SelfReferenceTargets = 1 });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.Notices).Contains("1 self-referenced file was modified.");
    }

    [Test]
    public async Task Release_WithEverySelfReferenceTarget_RecordsTheirNumber()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.Notices).Contains("3 self-referenced files were modified.");
    }
}
