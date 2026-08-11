// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;

/// <summary>
/// End-to-end tests of the <c>release</c> command's happy path, over a real repository with only the process
/// and platform boundaries faked (see <see cref="ReleaseHarness"/>).
/// </summary>
/// <remarks>
/// <para>The harness anchors the process's current directory and sets an environment variable, so these tests
/// cannot share the process with others that run at the same time.</para>
/// </remarks>
[NotInParallel]
internal sealed class ReleaseCommandTests
{
    // The repository starts one commit into the 2.3 prerelease line; the release commit makes it two,
    // and the Git height is the patch number.
    private const string ReleasedVersion = "2.3.2-preview";
    private const string ReleaseCommitMessage = $"Prepare release {ReleasedVersion} [skip ci]";
    private const string PostReleaseCommitMessage = $"Post-release updates for {ReleasedVersion} [skip ci]";

    [Test]
    public async Task Release_RunsPipelineTwice_ThenPushesAndPublishes()
    {
        using var harness = new ReleaseHarness();

        var exitCode = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);

        // The verification pass (Clean→Test) runs before the release commit exists; the artifact pass
        // (Restore→Pack) runs after it, so that what is packed carries the version that will be tagged.
        var steps = harness.Events.Select(x => x.Name).ToArray();
        await Assert.That(steps).IsEquivalentTo(
            ["restore", "build", "restore", "build", "pack", "nuget-push", "nuget-push", "nuget-push", "publish"]);
        await Assert.That(harness.Events[0].HeadMessage).IsEqualTo("Initial commit");
        await Assert.That(harness.Events[1].HeadMessage).IsEqualTo("Initial commit");
        await Assert.That(harness.Events[4].HeadMessage).IsEqualTo(ReleaseCommitMessage);
    }

    [Test]
    public async Task Release_CreatesReleaseCommit_BeforePacking()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        var pack = harness.Events.Single(x => x.Name == "pack");
        await Assert.That(pack.HeadMessage).IsEqualTo(ReleaseCommitMessage);
    }

    [Test]
    public async Task Release_PushesRepository_BeforePushingPackages()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        // Nothing has reached the remote while the artifacts are being built...
        await Assert.That(harness.Events.Single(x => x.Name == "pack").RemoteTipSha).IsNull();

        // ...and everything has, by the time the packages are pushed to NuGet.
        var head = harness.Repo.GetCommits(1)[0];
        await Assert.That(harness.Events.First(x => x.Name == "nuget-push").RemoteTipSha).IsEqualTo(head.Sha);
    }

    [Test]
    public async Task Release_TagsNothing_AndLeavesTaggingToTheServer()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        // The release object publishes the tag on the server; bv never creates one locally.
        await Assert.That(harness.Repo.TagNames.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Release_WithoutChangesToCommit_CreatesEmptyReleaseCommit()
    {
        using var harness = new ReleaseHarness(new() { Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        var commits = harness.Repo.GetCommits(2);
        await Assert.That(commits[0].Message).IsEqualTo(ReleaseCommitMessage);
        await Assert.That(commits[0].ChangedFiles.Count).IsEqualTo(0);
        await Assert.That(commits[1].Message).IsEqualTo("Initial commit");
    }

    [Test]
    public async Task Release_WithDogfooding_CommitsSelfReferenceUpdates_OnTopOfReleaseCommit()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        var commits = harness.Repo.GetCommits(2);
        await Assert.That(commits[0].Message).IsEqualTo(PostReleaseCommitMessage);
        await Assert.That(commits[0].ChangedFiles).IsEquivalentTo(
            [".config/dotnet-tools.json", "Directory.Packages.props", "global.json"]);
        await Assert.That(commits[1].Message).IsEqualTo(ReleaseCommitMessage);

        // The rewritten files carry the version this release produced, not the one they started at.
        foreach (var relativePath in commits[0].ChangedFiles)
        {
            await Assert.That(harness.ReadFile(relativePath)).Contains(ReleasedVersion).And.DoesNotContain(ReleaseHarness.PreviousVersion);
        }
    }

    [Test]
    public async Task Release_WithoutDogfooding_LeavesSelfReferencesAlone()
    {
        using var harness = new ReleaseHarness(new() { Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.ReadFile("global.json")).Contains(ReleaseHarness.PreviousVersion);
        await Assert.That(harness.Repo.GetCommits(1)[0].Message).IsEqualTo(ReleaseCommitMessage);
    }

    [Test]
    public async Task Release_WithHook_JoinsHookChangesToPostReleaseCommit()
    {
        using var harness = new ReleaseHarness(new() { WithHook = true });

        // ReSharper disable once AccessToDisposedClosure // False positive: the hook runs before the harness is disposed
        harness.HookBehavior = () => harness.WriteFile("docs/release-notes.md", $"Released {ReleasedVersion}.\n");

        _ = await harness.RunAsync().ConfigureAwait(false);

        // The hook runs after the artifacts are packed, and before the post-release commit it contributes to.
        var steps = harness.Events.Select(x => x.Name).ToArray();
        await Assert.That(Array.IndexOf(steps, "hook")).IsGreaterThan(Array.IndexOf(steps, "pack"));
        var commits = harness.Repo.GetCommits(1);
        await Assert.That(commits[0].Message).IsEqualTo(PostReleaseCommitMessage);
        await Assert.That(commits[0].ChangedFiles).Contains("docs/release-notes.md");
    }

    [Test]
    public async Task Release_WithoutHook_SkipsIt()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.AppRunner.Runs.Count).IsEqualTo(0);
        await Assert.That(harness.Events.Any(x => x.Name == "hook")).IsFalse();
    }

    [Test]
    public async Task Release_PublishesEveryProducedPackage_AndItsSymbols()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        var release = harness.Adapter.Release!;
        await Assert.That(release.IsPublished).IsTrue();
        var assets = release.PublishedAssets;
        var names = assets.Select(x => Path.GetFileName(x.Path)).ToArray();
        await Assert.That(names).IsEquivalentTo([
            "setup.exe",
            $"Test.Sdk.{ReleasedVersion}.nupkg",
            $"Test.Tool.{ReleasedVersion}.nupkg",
            $"Test.Lib.{ReleasedVersion}.nupkg",
            $"Test.Lib.{ReleasedVersion}.snupkg",
        ]);

        // Assets listed by an asset list carry its description and MIME type; packages get the defaults.
        var setup = assets.Single(x => x.Path.EndsWith("setup.exe", StringComparison.Ordinal));
        await Assert.That(setup.Description).IsEqualTo("Setup program");
        await Assert.That(setup.MimeType).IsEqualTo("application/vnd.microsoft.portable-executable");
        var package = assets.Single(x => x.Path.EndsWith($"Test.Sdk.{ReleasedVersion}.nupkg", StringComparison.Ordinal));
        await Assert.That(package.Description).IsEqualTo($"Test.Sdk.{ReleasedVersion}.nupkg");
        await Assert.That(package.MimeType).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Release_ReportsUnusableAssetListLines()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        var warnings = harness.Reporter.Messages.Where(x => x.Level == MessageLevel.Warning).Select(x => x.Message).ToArray();
        await Assert.That(warnings.Any(x => x.Contains("invalid line", StringComparison.Ordinal))).IsTrue();
        await Assert.That(warnings.Any(x => x.Contains("asset not found", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Release_PushesEveryPackage_ToTheConfiguredFeed()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        var pushes = harness.ProcessRunner.Runs.Where(x => x.Args.Contains("push")).ToArray();
        await Assert.That(pushes.Length).IsEqualTo(3);
        foreach (var push in pushes)
        {
            await Assert.That(push.Args).Contains("https://nuget.example.invalid/v3/index.json");
            await Assert.That(push.Args).Contains("test-api-key");
        }
    }

    [Test]
    public async Task Release_UsesCIBotIdentity_WhenTheRepositoryHasNone()
    {
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync().ConfigureAwait(false);

        var commit = harness.Repo.GetCommits(1)[0];
        await Assert.That(commit.CommitterName).IsEqualTo("Buildvana Test Bot");
        await Assert.That(commit.CommitterEmail).IsEqualTo("bot@buildvana.invalid");
    }

    [Test]
    public async Task Release_KeepsConfiguredIdentity_WhenTheRepositoryHasOne()
    {
        using var harness = new ReleaseHarness();
        harness.Repo.SetCommitterIdentity("Local Committer", "local@example.invalid");

        _ = await harness.RunAsync().ConfigureAwait(false);

        var commit = harness.Repo.GetCommits(1)[0];
        await Assert.That(commit.CommitterName).IsEqualTo("Local Committer");
        await Assert.That(commit.CommitterEmail).IsEqualTo("local@example.invalid");
    }

    // A bump to an unstable line writes the configured prerelease tag into the version file, so that the
    // file keeps saying which tag the line carries rather than a bare "-".
    [Test]
    [Arguments("minor", "2.4-preview")]
    [Arguments("major", "3.0-preview")]
    [Arguments("stable", "2.3")]
    public async Task Release_WithBump_AppliesVersionSpecChange_InReleaseCommit(string bump, string expectedSpec)
    {
        using var harness = new ReleaseHarness(new() { Dogfood = false });

        _ = await harness.RunAsync("--bump", bump).ConfigureAwait(false);

        await Assert.That(harness.ReadFile("VERSION").Trim()).IsEqualTo(expectedSpec);
        var commit = harness.Repo.GetCommits(1)[0];
        await Assert.That(commit.ChangedFiles).IsEquivalentTo(["VERSION"]);
    }

    // The version bv publishes and the version a build computes come from the same Git height, so they
    // agree only as long as the height is read from the release commit as it will finally stand. A bump
    // is what puts that at risk: it changes the version file, which is the very input the height is
    // computed from, and 0 is not a height at all — the calculator reserves it for a version line with
    // no committed history, which is what a bumped line looks like until its commit exists.
    [Test]
    [Arguments("none", "2.3.2-preview")]
    [Arguments("minor", "2.4.1-preview")]
    [Arguments("major", "3.0.1-preview")]
    [Arguments("stable", "2.3.2")]
    public async Task Release_PublishesTheVersionItsArtifactsAreBuiltWith(string bump, string expectedVersion)
    {
        using var harness = new ReleaseHarness(new() { Dogfood = false });

        _ = await harness.RunAsync("--bump", bump).ConfigureAwait(false);

        await Assert.That(harness.Repo.GetCommits(1)[0].Message).IsEqualTo($"Prepare release {expectedVersion} [skip ci]");
        await Assert.That(harness.ComputeVersion()).IsEqualTo(expectedVersion);
    }

    // What the previous test asserts as a cause has a consequence, and it is the consequence that a real
    // release shows first. Self-references are rewritten from the packages the pack step produced, which
    // are discovered by file name, and a name built from the wrong version matches nothing. Nothing is
    // then rewritten, no error is reported, and the release completes — with the whole point of dogfooding
    // quietly skipped. So the bumped version is asserted here all the way into the files.
    [Test]
    public async Task Release_WithBump_AndDogfooding_RewritesSelfReferencesToTheBumpedVersion()
    {
        const string bumpedVersion = "2.4.1-preview";
        using var harness = new ReleaseHarness();

        _ = await harness.RunAsync("--bump", "minor").ConfigureAwait(false);

        var commits = harness.Repo.GetCommits(2);
        await Assert.That(commits[1].Message).IsEqualTo($"Prepare release {bumpedVersion} [skip ci]");
        await Assert.That(commits[0].Message).IsEqualTo($"Post-release updates for {bumpedVersion} [skip ci]");
        await Assert.That(commits[0].ChangedFiles).IsEquivalentTo(
            [".config/dotnet-tools.json", "Directory.Packages.props", "global.json"]);
        foreach (var relativePath in commits[0].ChangedFiles)
        {
            await Assert.That(harness.ReadFile(relativePath)).Contains(bumpedVersion).And.DoesNotContain(ReleaseHarness.PreviousVersion);
        }
    }

    [Test]
    public async Task Release_WithoutBump_LeavesVersionFileAlone()
    {
        using var harness = new ReleaseHarness(new() { Dogfood = false });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.ReadFile("VERSION").Trim()).IsEqualTo("2.3-");
    }

    [Test]
    public async Task Release_OnStableRelease_ShipsPublicApis()
    {
        using var harness = new ReleaseHarness(new()
        {
            VersionSpec = "2.3",
            Dogfood = false,
            CheckPublicApi = false,
            UnshippedPublicApi = "#nullable enable\nTest.Thing\n",
        });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.ReadFile($"{ReleaseHarness.PublicApiDirectory}/PublicAPI.Shipped.txt")).Contains("Test.Thing");
        await Assert.That(harness.ReadFile($"{ReleaseHarness.PublicApiDirectory}/PublicAPI.Unshipped.txt")).DoesNotContain("Test.Thing");
        var commit = harness.Repo.GetCommits(1)[0];
        await Assert.That(commit.ChangedFiles).IsEquivalentTo([
            $"{ReleaseHarness.PublicApiDirectory}/PublicAPI.Shipped.txt",
            $"{ReleaseHarness.PublicApiDirectory}/PublicAPI.Unshipped.txt",
        ]);
    }

    [Test]
    public async Task Release_OnPrerelease_LeavesPublicApisUnshipped()
    {
        using var harness = new ReleaseHarness(new()
        {
            Dogfood = false,
            CheckPublicApi = false,
            UnshippedPublicApi = "#nullable enable\nTest.Thing\n",
        });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.ReadFile($"{ReleaseHarness.PublicApiDirectory}/PublicAPI.Unshipped.txt")).Contains("Test.Thing");
        await Assert.That(harness.Repo.GetCommits(1)[0].ChangedFiles.Count).IsEqualTo(0);
    }

    // An additive public API forces a minor bump, and a minor bump moves the version onto a prerelease
    // line — which is why the public API files are then left unshipped, even though the release started
    // out stable: what gets released is no longer the stable version whose API they would document.
    [Test]
    public async Task Release_WithAdditivePublicApi_BumpsMinor_AndLeavesApisUnshipped()
    {
        using var harness = new ReleaseHarness(new()
        {
            VersionSpec = "2.3",
            Dogfood = false,
            UnshippedPublicApi = "#nullable enable\nTest.Thing\n",
        });

        _ = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(harness.ReadFile("VERSION").Trim()).IsEqualTo("2.4-preview");
        await Assert.That(harness.ReadFile($"{ReleaseHarness.PublicApiDirectory}/PublicAPI.Unshipped.txt")).Contains("Test.Thing");
        await Assert.That(harness.Repo.GetCommits(1)[0].ChangedFiles).IsEquivalentTo(["VERSION"]);
    }
}
