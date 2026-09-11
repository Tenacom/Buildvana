// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;

/// <summary>
/// End-to-end tests of how the <c>release</c> command applies the changelog-update policy
/// (<c>release.changelogUpdates</c>) and the empty-section substitute (<c>release.emptyChangelog</c>).
/// </summary>
[NotInParallel]
internal sealed class ReleaseCommandChangelogTests
{
    private const string ReleasedVersion = "2.3.2-preview";

    private const string ChangelogWithChanges = """
        # Changelog

        ## Unreleased changes

        ### New features

        - Something new.

        ## [2.3.0-preview](https://git.example.invalid/tenacom/test-repo/releases/tag/2.3.0-preview) (2026-01-01)

        - Something released.

        """;

    private const string ChangelogWithFileLink = """
        # Changelog

        ## Unreleased changes

        ### New features

        - A hook [runs from the home directory](docs/hooks.md#the-build-environment).

        ## [2.3.0-preview](https://git.example.invalid/tenacom/test-repo/releases/tag/2.3.0-preview) (2026-01-01)

        - Something released, see [the page](docs/released.md).

        """;

    private const string ChangelogWithOutsideLink = """
        # Changelog

        ## Unreleased changes

        ### New features

        - Something new, see [the page](../outside.md).

        ## [2.3.0-preview](https://git.example.invalid/tenacom/test-repo/releases/tag/2.3.0-preview) (2026-01-01)

        - Something released.

        """;

    private const string EmptyChangelog = """
        # Changelog

        ## Unreleased changes

        ### New features

        ## [2.3.0-preview](https://git.example.invalid/tenacom/test-repo/releases/tag/2.3.0-preview) (2026-01-01)

        - Something released.

        """;

    [Test]
    [Arguments("all", "2.3-")]
    [Arguments("stable", "2.3")]
    public async Task Release_WhenPolicyApplies_MovesUnreleasedChangesToNewSection(string policy, string versionSpec)
    {
        using var harness = new ReleaseHarness(new()
        {
            VersionSpec = versionSpec,
            ChangelogUpdates = policy,
            Changelog = ChangelogWithChanges,
            Dogfood = false,
        });

        _ = await harness.RunAsync().ConfigureAwait(false);

        var changelog = harness.ReadFile("CHANGELOG.md");
        var expectedVersion = versionSpec == "2.3-" ? ReleasedVersion : "2.3.2";
        var expectedTitle = $"## [{expectedVersion}](https://git.example.invalid/tenacom/test-repo/releases/tag/{expectedVersion})";
        await Assert.That(changelog).Contains(expectedTitle);
        await Assert.That(changelog).Contains("- Something new.");
        await Assert.That(harness.Repo.GetCommits(1)[0].ChangedFiles).IsEquivalentTo(["CHANGELOG.md"]);
    }

    [Test]
    public async Task Release_OnPrereleaseWithStablePolicy_LeavesChangelogAlone()
    {
        using var harness = new ReleaseHarness(new() { ChangelogUpdates = "stable", Changelog = ChangelogWithChanges, Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.ReadFile("CHANGELOG.md")).IsEqualTo(ChangelogWithChanges);
        await Assert.That(harness.Repo.GetCommits(1)[0].ChangedFiles.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Release_WithNonePolicy_LeavesChangelogAlone_EvenOnStableRelease()
    {
        using var harness = new ReleaseHarness(new()
        {
            VersionSpec = "2.3",
            ChangelogUpdates = "none",
            Changelog = ChangelogWithChanges,
            Dogfood = false,
        });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.ReadFile("CHANGELOG.md")).IsEqualTo(ChangelogWithChanges);
    }

    [Test]
    public async Task Release_WithoutChangelogFile_SkipsTheUpdate()
    {
        using var harness = new ReleaseHarness(new() { ChangelogUpdates = "all", Dogfood = false });

        var exitCode = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(File.Exists(Path.Combine(harness.Repo.RootPath, "CHANGELOG.md"))).IsFalse();
    }

    [Test]
    public async Task Release_WithEmptySectionAndNoSubstitute_Fails()
    {
        using var harness = new ReleaseHarness(new() { ChangelogUpdates = "all", Changelog = EmptyChangelog, Dogfood = false });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("release.emptyChangelog");

        // The failure happens before anything is built or committed.
        await Assert.That(harness.Events.Count).IsEqualTo(0);
        await Assert.That(harness.Repo.GetCommits(1)[0].Message).IsEqualTo("Initial commit");
    }

    [Test]
    public async Task Release_WithEmptySectionAndSubstitute_UsesTheSubstitute()
    {
        using var harness = new ReleaseHarness(new()
        {
            ChangelogUpdates = "all",
            Changelog = EmptyChangelog,
            EmptyChangelog = "This release contains no user-visible changes.",
            Dogfood = false,
        });

        var exitCode = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(harness.ReadFile("CHANGELOG.md")).Contains("This release contains no user-visible changes.");
    }

    [Test]
    public async Task Release_UpdatesNewSectionTitle_ToTheVersionActuallyReleased()
    {
        using var harness = new ReleaseHarness(new() { ChangelogUpdates = "all", Changelog = ChangelogWithChanges, Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        // The heading is written after the build, when the version is final: it names the released
        // version, and no other.
        var changelog = harness.ReadFile("CHANGELOG.md");
        await Assert.That(changelog).Contains($"## [{ReleasedVersion}]");
        await Assert.That(changelog).DoesNotContain("## [2.3.1-preview]");
    }

    [Test]
    public async Task Release_PinsFileLinksOfNewSection_ToTheTagActuallyReleased()
    {
        using var harness = new ReleaseHarness(new() { ChangelogUpdates = "all", Changelog = ChangelogWithFileLink, Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        // The links are pinned once the release commit exists and the version is final: the tag they name is
        // the one released, not the one computed before the commit bumped the Git height. The section released
        // before this one is left as it is.
        var changelog = harness.ReadFile("CHANGELOG.md");
        const string pinnedUrl = $"https://git.example.invalid/tenacom/test-repo/blob/{ReleasedVersion}/docs/hooks.md#the-build-environment";
        await Assert.That(changelog).Contains($"[runs from the home directory]({pinnedUrl})");
        await Assert.That(changelog).DoesNotContain("blob/2.3.1-preview/");
        await Assert.That(changelog).Contains("[the page](docs/released.md)");
    }

    [Test]
    public async Task Release_WithLinkOutsideRepository_FailsNamingTheTarget()
    {
        using var harness = new ReleaseHarness(new()
        {
            ChangelogUpdates = "all",
            Changelog = ChangelogWithOutsideLink,
            Dogfood = false,
        });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).IsEqualTo("CHANGELOG.md links '../outside.md', which is not a path to a file in the repository.");

        // The links are pinned once the version is final, after the build, so the failure comes with the
        // release commit in place. The rollback undoes that commit.
        await Assert.That(harness.Events.Any(x => x.Name == "pack")).IsTrue();
        await Assert.That(harness.Repo.GetCommits(1)[0].Message).IsEqualTo("Initial commit");
    }
}
