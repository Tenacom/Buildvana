// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;

/// <summary>
/// End-to-end tests of what the <c>release</c> command refuses to do, and of what it undoes when a release
/// fails after the repository has already been changed.
/// </summary>
[NotInParallel]
internal sealed class ReleaseCommandFailureTests
{
    private const string ReleasedVersion = "2.3.2-preview";

    [Test]
    public async Task Release_OnExistingTag_FailsBeforeBuildingArtifacts()
    {
        using var harness = new ReleaseHarness();
        TagAnotherBranch(harness);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains($"Tag '{ReleasedVersion}' already exists");

        // The check gates the artifact pass: the verification pass has run, the artifact pass has not.
        await Assert.That(harness.Events.Select(x => x.Name)).IsEquivalentTo(["restore", "build"]);
    }

    [Test]
    public async Task Release_OnExistingTag_UndoesTheReleaseCommit()
    {
        using var harness = new ReleaseHarness();
        var initialSha = harness.Repo.HeadSha;
        TagAnotherBranch(harness);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        _ = await Assert.That(Act).Throws<BuildFailedException>();

        // The release commit is created before the tag check, and rolled back when the release is disposed.
        await Assert.That(harness.Repo.GetCommits(1)[0].Sha).IsEqualTo(initialSha);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsNull();
    }

    [Test]
    public async Task Release_WhenPublicationFails_ResetsAndForcePushesTheBranch()
    {
        using var harness = new ReleaseHarness(new()
        {
            OnPublishing = () => throw new BuildFailedException("The server said no."),
        });
        var initialSha = harness.Repo.HeadSha;

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        _ = await Assert.That(Act).Throws<BuildFailedException>();

        // Both the release commit and the post-release commit are gone, and the remote has been reset to
        // where it was before the release pushed to it.
        var head = harness.Repo.GetCommits(1)[0];
        await Assert.That(head.Sha).IsEqualTo(initialSha);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(initialSha);

        // What the repository did is undone; what the server did is not, because it never got that far:
        // the publication rollback is only armed once the publication itself has succeeded.
        var release = harness.Adapter.Release!;
        await Assert.That(release.IsPublished).IsFalse();
        await Assert.That(release.IsPublishUndone).IsFalse();
    }

    [Test]
    public async Task Release_OnLocalBuild_Fails()
    {
        using var harness = new ReleaseHarness(new() { CloudBuild = false });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("cloud build");

        // The preliminary checks run before the verification pass, so a release that can never succeed
        // costs nothing: no clean, no build, no test run.
        await Assert.That(harness.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Release_WithoutAnyCommitterIdentity_FailsBeforeBuilding()
    {
        using var harness = new ReleaseHarness(new() { WithBotIdentity = false });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("committer identity");
        await Assert.That(harness.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Release_WhenHookFails_LeavesTheRepositoryUnchanged()
    {
        using var harness = new ReleaseHarness(new() { WithHook = true });
        var initialSha = harness.Repo.HeadSha;
        harness.HookBehavior = () => throw new BuildFailedException("The hook said no.");

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        _ = await Assert.That(Act).Throws<BuildFailedException>();

        await Assert.That(harness.Repo.GetCommits(1)[0].Sha).IsEqualTo(initialSha);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsNull();
    }

    // Tags the version about to be released on a commit outside the release branch's history. A tag on the
    // branch itself would be caught earlier, by the versioning consistency check: it would be the latest
    // version in history, and the version being released would no longer be higher than it. What is left
    // for the tag check to catch is exactly this: a tag that the branch cannot see.
    private static void TagAnotherBranch(ReleaseHarness harness)
    {
        var releaseBranch = harness.Repo.CurrentBranchName;
        harness.Repo.CheckoutNewBranch("abandoned");
        harness.Repo.CommitAll("Abandoned release");
        harness.Repo.CreateTag(ReleasedVersion);
        harness.Repo.Checkout(releaseBranch);
    }
}
