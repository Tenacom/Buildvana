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
        await Assert.That(harness.Events.Any(x => x.Name == "pack")).IsFalse();
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

        // The section is written before the artifact pass, when the version is already final: the title
        // names the released version, and no other.
        var changelog = harness.ReadFile("CHANGELOG.md");
        await Assert.That(changelog).Contains($"## [{ReleasedVersion}]");
        await Assert.That(changelog).DoesNotContain("## [2.3.1-preview]");
    }
}
