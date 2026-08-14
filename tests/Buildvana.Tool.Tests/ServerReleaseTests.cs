// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Buildvana.Tool.Services.ServerAdapters;

/// <summary>
/// Tests of <see cref="ServerRelease"/> driven directly, over the same repository and service graph a real
/// release runs on (see <see cref="ReleaseHarness"/>).
/// </summary>
/// <remarks>
/// <para>The release command walks one path through this class; the class is a contract for every adapter,
/// so what it refuses and what it records are asserted here rather than through the one caller that exists
/// today. The order its methods may be called in is part of that contract: a commit after a push, or a
/// post-release commit before the release commit, would leave a release the rollback cannot undo, and each
/// guard is what turns that into an error at the call that got it wrong.</para>
/// <para>The harness anchors the process's current directory and sets an environment variable, so these tests
/// cannot share the process with others that run at the same time.</para>
/// </remarks>
[NotInParallel]
internal sealed class ServerReleaseTests
{
    [Test]
    public async Task EnsureReleaseCommit_AfterPushingUpdates_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            release.PushUpdates();

            void Act() => release.EnsureReleaseCommit();

            var exception = await Assert.That(Act).Throws<InvalidOperationException>();
            await Assert.That(exception!.Message).Contains("already been pushed");
        }
    }

    [Test]
    public async Task UpdateRepository_AfterPushingUpdates_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            release.PushUpdates();
            harness.WriteFile("docs/late.md", "Too late.\n");

            void Act() => release.UpdateRepository("docs/late.md");

            var exception = await Assert.That(Act).Throws<InvalidOperationException>();
            await Assert.That(exception!.Message).Contains("already been pushed");
        }
    }

    [Test]
    public async Task UpdateRepository_AfterAPostReleaseCommit_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            AddPostReleaseCommit(harness, release, 1);
            harness.WriteFile("docs/late.md", "Too late.\n");

            void Act() => release.UpdateRepository("docs/late.md");

            var exception = await Assert.That(Act).Throws<InvalidOperationException>();
            await Assert.That(exception!.Message).Contains("after a post-release commit");
        }
    }

    [Test]
    public async Task AddPostReleaseCommit_AfterPushingUpdates_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            release.PushUpdates();
            harness.WriteFile("docs/late.md", "Too late.\n");

            void Act() => release.AddPostReleaseCommit("Post-release updates [skip ci]", "docs/late.md");

            var exception = await Assert.That(Act).Throws<InvalidOperationException>();
            await Assert.That(exception!.Message).Contains("already been pushed");
        }
    }

    [Test]
    public async Task AddPostReleaseCommit_BeforeTheReleaseCommit_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            harness.WriteFile("docs/early.md", "Too early.\n");

            void Act() => release.AddPostReleaseCommit("Post-release updates [skip ci]", "docs/early.md");

            var exception = await Assert.That(Act).Throws<InvalidOperationException>();
            await Assert.That(exception!.Message).Contains("before the release commit");
        }
    }

    // Nothing to push is not an error - a release may legitimately change no file at all - so it is recorded
    // and the release goes on. The line matters because the push is otherwise invisible: without it, a run
    // that pushed nothing and a run that pushed the release commit read exactly alike.
    [Test]
    public async Task PushUpdates_WithoutAReleaseCommit_RecordsThatThereIsNothingToPush()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.PushUpdates();
        }

        await Assert.That(harness.Notices).Contains("Repository unchanged, no commit to push.");
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsNull();
    }

    [Test]
    [Arguments(0, "with no assets.")]
    [Arguments(1, "with 1 asset.")]
    [Arguments(2, "with 2 assets.")]
    public async Task PublishAsync_RecordsTheNumberOfAssets(int assetCount, string expectedEnding)
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            for (var i = 0; i < assetCount; i++)
            {
                release.AddAsset(string.Create(CultureInfo.InvariantCulture, $"artifacts/asset-{i}.bin"));
            }

            await release.PublishAsync().ConfigureAwait(false);
        }

        await Assert.That(harness.Notices).Contains($"Published release {harness.ComputeVersion()} {expectedEnding}");
    }

    // Every commit the release made is walked back by one rollback action, and recorded by one line naming
    // how many: the release commit is always there, the post-release commits are however many the caller
    // added. The release command adds at most one of them; the class allows any number, so the ladder is
    // asserted here rather than through the one caller.
    [Test]
    [Arguments(0, "Undid the release commit.")]
    [Arguments(1, "Undid the release commit and 1 post-release commit.")]
    [Arguments(2, "Undid the release commit and 2 post-release commits.")]
    public async Task DisposeAsync_RecordsEveryUndoneCommitAtOnce(int postReleaseCommits, string expectedNotice)
    {
        using var harness = new ReleaseHarness();
        var initialSha = harness.Repo.HeadSha;
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            for (var i = 0; i < postReleaseCommits; i++)
            {
                AddPostReleaseCommit(harness, release, i + 1);
            }
        }

        await Assert.That(harness.Notices).Contains(expectedNotice);
        await Assert.That(harness.Repo.GetCommits(1)[0].Sha).IsEqualTo(initialSha);
    }

    // A release object over the harness's repository, with a committer identity for the commits it makes:
    // the command sets one from the server adapter before creating the release, and nothing else does.
    private static async Task<ServerRelease> StartReleaseAsync(ReleaseHarness harness)
    {
        harness.Repo.SetCommitterIdentity("Buildvana Test Bot", "bot@buildvana.invalid");
        return await harness.Adapter.CreateReleaseAsync().ConfigureAwait(false);
    }

    private static void AddPostReleaseCommit(ReleaseHarness harness, ServerRelease release, int ordinal)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"docs/post-release-{ordinal}.md");
        harness.WriteFile(path, string.Create(CultureInfo.InvariantCulture, $"Post-release change #{ordinal}.\n"));
        release.AddPostReleaseCommit(
            string.Create(CultureInfo.InvariantCulture, $"Post-release updates #{ordinal} [skip ci]"),
            path);
    }
}
