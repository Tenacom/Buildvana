// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
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
    public async Task Release_OnExistingTag_FailsBeforeBuilding()
    {
        using var harness = new ReleaseHarness();
        TagAnotherBranch(harness);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        await Assert.That(Act)
            .Throws<BuildFailedException>()
            .WithMessageContaining($"Tag '{ReleasedVersion}' already exists", StringComparison.Ordinal);

        // The check gates the pipeline run: no step runs, Clean included.
        await Assert.That(harness.PipelineSteps.Count).IsEqualTo(0);
        await Assert.That(harness.Events.Count).IsEqualTo(0);
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
        using var harness = new ReleaseHarness(new() {
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

        // The tag is gone too, locally and on the remote, and the host is asked to undo what it created.
        await Assert.That(harness.Repo.TagNames.Count).IsEqualTo(0);
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
        var publisher = harness.Publisher!;
        await Assert.That(publisher.IsPublished).IsFalse();
        await Assert.That(publisher.IsUndone).IsTrue();
    }

    [Test]
    public async Task Release_WithoutRepositoryUrl_FailsBeforeBuilding()
    {
        using var harness = new ReleaseHarness(new() { RepositoryUrl = null });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        await Assert.That(Act).Throws<BuildFailedException>().WithMessageContaining("repository.url", StringComparison.Ordinal);

        // The preliminary checks run before the pipeline, so a release that can never succeed costs
        // nothing: no clean, no build, no test run.
        await Assert.That(harness.PipelineSteps.Count).IsEqualTo(0);
        await Assert.That(harness.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Release_OnAnUnknownHost_WithoutAllowUnknownHost_FailsBeforeBuilding()
    {
        using var harness = new ReleaseHarness(new() { UnknownHost = true });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        await Assert.That(Act).Throws<BuildFailedException>().WithMessageContaining("release.allowUnknownHost", StringComparison.Ordinal);
        await Assert.That(harness.PipelineSteps.Count).IsEqualTo(0);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsNull();
    }

    // A clone that lacks a tag of the release remote would check the versions against an incomplete history.
    // The fetch brings the tag in, and the check sees it.
    [Test]
    public async Task Release_WhenTheReleaseRemoteHasAHigherTag_FailsBeforeBuilding()
    {
        using var harness = new ReleaseHarness();
        harness.Repo.PushCurrentBranch();
        harness.Repo.CreateRemoteTag("2.4.0");

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        await Assert.That(Act).Throws<BuildFailedException>().WithMessageContaining("lower than latest", StringComparison.Ordinal);
        await Assert.That(harness.Repo.TagNames).IsEquivalentTo(["2.4.0"]);
        await Assert.That(harness.PipelineSteps.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Release_WithoutAnyCommitterIdentity_FailsBeforeBuilding()
    {
        using var harness = new ReleaseHarness(new() { WithBotIdentity = false });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        await Assert.That(Act).Throws<BuildFailedException>().WithMessageContaining("committer identity", StringComparison.Ordinal);
        await Assert.That(harness.PipelineSteps.Count).IsEqualTo(0);
        await Assert.That(harness.Events.Count).IsEqualTo(0);
    }

    // A version line no commit carries has a Git height of 0, which is not a height at all: publishing it
    // would tag a commit that does not reproduce the published version, since a build of that commit would
    // count from 1 as soon as the version file reached it.
    [Test]
    public async Task Release_WithUncommittedVersionFile_Fails()
    {
        using var harness = new ReleaseHarness(new() { CommitVersionFile = false });
        var initialSha = harness.Repo.HeadSha;

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        Debug.Assert(exception is not null, "Throws returns null only inside Assert.Multiple.");
        await Assert.That(exception.Message).Contains("Git height of 0");
        await Assert.That(exception.Message).Contains("Commit VERSION");

        // It is caught before anything is packed, and the release commit is rolled back.
        await Assert.That(harness.Events.Any(x => x.Name == "pack")).IsFalse();
        await Assert.That(harness.Repo.GetCommits(1)[0].Sha).IsEqualTo(initialSha);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsNull();
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
