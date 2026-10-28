// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Buildvana.Tool.Services.Release;

/// <summary>
/// Tests of <see cref="ServerRelease"/> driven directly, over the same repository and service graph a real
/// release runs on (see <see cref="ReleaseHarness"/>).
/// </summary>
/// <remarks>
/// <para>The release command walks one path through this class; the class is a contract for every host,
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

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("already been pushed", StringComparison.Ordinal);
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

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("already been pushed", StringComparison.Ordinal);
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

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("after a post-release commit", StringComparison.Ordinal);
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

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("already been pushed", StringComparison.Ordinal);
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

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("before the release commit", StringComparison.Ordinal);
        }
    }

    // The release tag names the release commit, so there is nothing to tag before the commit exists.
    [Test]
    public async Task PushUpdates_WithoutAReleaseCommit_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            void Act() => release.PushUpdates();

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("before the release commit", StringComparison.Ordinal);
        }

        await Assert.That(harness.Repo.GetRemoteTipSha()).IsNull();
    }

    // The tag names the release commit, not the post-release commit that the branch tip carries.
    [Test]
    public async Task PushUpdates_TagsTheReleaseCommit_AndPushesTheTag()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            var releaseCommitSha = release.ReleaseCommitSha;
            var tag = harness.ComputeVersion();
            AddPostReleaseCommit(harness, release, 1);

            release.PushUpdates();

            await Assert.That(harness.Repo.TagNames).IsEquivalentTo([tag]);
            await Assert.That(harness.Repo.GetRemoteTagSha(tag)).IsEqualTo(releaseCommitSha);
            await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(harness.Repo.HeadSha);
        }
    }

    // The publisher checks the assets before the push, so an asset added after it would go unchecked.
    [Test]
    public async Task AddAsset_AfterPushingUpdates_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            release.PushUpdates();

            void Act() => release.AddAsset("artifacts/late.bin");

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("already been pushed", StringComparison.Ordinal);
        }
    }

    [Test]
    public async Task PushUpdates_HasThePublisherCheckTheAssets()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            release.AddAsset("artifacts/setup.exe", "Setup program", "application/vnd.microsoft.portable-executable");

            release.PushUpdates();

            ReleaseAsset[] expected = [new("artifacts/setup.exe", "Setup program", "application/vnd.microsoft.portable-executable")];
            var publisher = await Assert.That(harness.Publisher).IsNotNull();
            await Assert.That(publisher.CheckedAssets).IsEquivalentTo(expected);
        }
    }

    [Test]
    public async Task PublishAsync_BeforePushingUpdates_Throws()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();

            Task Act() => release.PublishAsync();

            await Assert.That(Act)
                .Throws<InvalidOperationException>()
                .WithMessageContaining("before pushing updates", StringComparison.Ordinal);
            var publisher = await Assert.That(harness.Publisher).IsNotNull();
            await Assert.That(publisher.IsPublished).IsFalse();
        }
    }

    [Test]
    public async Task PublishAsync_PublishesOnTheTag_AtTheReleaseCommit()
    {
        using var harness = new ReleaseHarness();
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            release.PushUpdates();
            await release.PublishAsync().ConfigureAwait(false);

            var publisher = await Assert.That(harness.Publisher).IsNotNull();
            await Assert.That(publisher.IsBegun).IsTrue();
            await Assert.That(publisher.PublishedTag).IsEqualTo(harness.ComputeVersion());
            await Assert.That(publisher.PublishedSha).IsEqualTo(release.ReleaseCommitSha);
        }

        // A published release is final: disposing it undoes nothing.
        await Assert.That(harness.Publisher?.IsUndone).IsFalse();
        await Assert.That(harness.Repo.TagNames.Count).IsEqualTo(1);
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
            release.EnsureReleaseCommit();
            for (var i = 0; i < assetCount; i++)
            {
                release.AddAsset(string.Create(CultureInfo.InvariantCulture, $"artifacts/asset-{i}.bin"));
            }

            release.PushUpdates();
            await release.PublishAsync().ConfigureAwait(false);
        }

        await Assert.That(harness.Notices).Contains($"Published release {harness.ComputeVersion()} {expectedEnding}");
    }

    // The tag goes before the commits it names, locally and on the remote, and the host-side release goes last.
    [Test]
    public async Task DisposeAsync_AfterPushingUpdates_DeletesTheTag_AndUndoesThePublisher()
    {
        using var harness = new ReleaseHarness();
        var initialSha = harness.Repo.HeadSha;
        var release = await StartReleaseAsync(harness).ConfigureAwait(false);
        string tag;
        await using (release.ConfigureAwait(false))
        {
            release.EnsureReleaseCommit();
            tag = harness.ComputeVersion();
            release.PushUpdates();
        }

        await Assert.That(harness.Repo.TagNames.Count).IsEqualTo(0);
        await Assert.That(harness.Repo.GetRemoteTagSha(tag)).IsNull();
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(initialSha);
        var publisher = await Assert.That(harness.Publisher).IsNotNull();
        await Assert.That(publisher.IsUndone).IsTrue();
        var notices = harness.Notices.ToArray();
        await Assert.That(Array.IndexOf(notices, $"Deleted tag '{tag}' from 'origin'."))
            .IsLessThan(Array.IndexOf(notices, "Undid the release commit."));
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
    // the command sets one before beginning the release, and nothing else does.
    private static async Task<ServerRelease> StartReleaseAsync(ReleaseHarness harness)
    {
        harness.Repo.SetCommitterIdentity("Buildvana Test Bot", "bot@buildvana.invalid");
        return await harness.BeginReleaseAsync().ConfigureAwait(false);
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
