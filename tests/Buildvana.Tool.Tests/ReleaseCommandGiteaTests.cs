// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using TUnit.Assertions.Enums;

/// <summary>
/// End-to-end tests of the <c>release</c> command on a real Gitea host, whose API answers from a table
/// (see <see cref="ReleaseHarnessOptions.GiteaApi"/>).
/// </summary>
/// <remarks>
/// <para>The repository URL of the harness is not <c>gitea.com</c>, so the host stands for a self-hosted
/// instance, with its API under <c>/api/v1</c>.</para>
/// </remarks>
[NotInParallel]
internal sealed class ReleaseCommandGiteaTests
{
    private const string ReleasedVersion = "2.3.2-preview";
    private const string ApiRepositoryUrl = "https://git.example.invalid/api/v1/repos/tenacom/test-repo";
    private const string ReleaseUrl = ApiRepositoryUrl + "/releases/42";

    [Test]
    public async Task Release_ChecksTheRepository_CreatesADraftOnTheTag_UploadsTheAssets_AndPublishesIt()
    {
        var api = Answering(new StubHttpAnswer(HttpStatusCode.Created, """{"id":1}"""));
        using var harness = new ReleaseHarness(new() { GiteaApi = api });

        var exitCode = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        var requests = api.Requests;
        await Assert.That(requests[0].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(requests[0].Url).IsEqualTo(ApiRepositoryUrl);

        // The draft goes on the pushed tag, then one upload per asset, then the update that publishes the draft.
        var releaseCommitSha = harness.Repo.GetCommits(2)[1].Sha;
        var draft = requests[1];
        await Assert.That(draft.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(draft.Url).IsEqualTo(ApiRepositoryUrl + "/releases");
        await Assert.That(draft.Body).Contains($"\"tag_name\":\"{ReleasedVersion}\",\"target_commitish\":\"{releaseCommitSha}\"");
        await Assert.That(draft.Body).Contains("\"draft\":true");
        var uploads = requests.Where(static x => x.Url.StartsWith(ReleaseUrl + "/assets?name=", StringComparison.Ordinal)).ToArray();
        await Assert.That(uploads.Length).IsEqualTo(5);
        await Assert.That(uploads.Select(static x => x.Url)).Contains(ReleaseUrl + "/assets?name=setup.exe");
        var update = requests[^1];
        await Assert.That(update.Method).IsEqualTo(HttpMethod.Patch);
        await Assert.That(update.Url).IsEqualTo(ReleaseUrl);
        await Assert.That(update.Body).Contains("\"draft\":false");
        await Assert.That(update.Body).Contains("https://git.example.invalid/tenacom/test-repo/src/branch/release/CHANGELOG.md");
        await Assert.That(requests.All(static x => x.Headers["Authorization"] == "token test-token")).IsTrue();
        await Assert.That(harness.Reporter.Messages.Select(static x => x.Message))
            .Contains("Fallback Git credentials provided by Gitea (protocol username: 'token').");
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsEqualTo(releaseCommitSha);
    }

    // The checks run before the release commit, so a refusal leaves nothing to roll back.
    [Test]
    [Arguments(false, true, "cannot push to tenacom/test-repo")]
    [Arguments(true, false, "The releases of tenacom/test-repo are disabled")]
    public async Task Release_WithoutPushAccess_OrWithReleasesDisabled_FailsBeforeTheReleaseCommit(
        bool canPush,
        bool hasReleases,
        string expectedMessage)
    {
        var api = Answering(new StubHttpAnswer(HttpStatusCode.Created, """{"id":1}"""), canPush: canPush, hasReleases: hasReleases);
        using var harness = new ReleaseHarness(new() { GiteaApi = api });
        var remoteTip = harness.Repo.GetRemoteTipSha();

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains(expectedMessage);
        await Assert.That(api.Requests.Count).IsEqualTo(1);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(remoteTip);
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // The assets are checked before the push, so a refused asset reaches neither the remote nor the feed.
    [Test]
    public async Task Release_WithTwoAssetsOfTheSameFileName_FailsBeforeThePush()
    {
        var api = Answering(new StubHttpAnswer(HttpStatusCode.Created, """{"id":1}"""));
        using var harness = new ReleaseHarness(new() { GiteaApi = api, WithHook = true });
        var remoteTip = harness.Repo.GetRemoteTipSha();
        var artifactsPath = harness.ArtifactsPath;

        harness.HookBehavior = () => {
            var directory = Directory.CreateDirectory(Path.Combine(artifactsPath, "other")).FullName;
            var path = Path.Combine(directory, "setup.exe");
            File.WriteAllText(path, "fake setup");
            File.WriteAllText(
                Path.Combine(artifactsPath, "Other.assets.txt"),
                $"{path}\tapplication/vnd.microsoft.portable-executable\tOther setup program\n");
        };

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).IsEqualTo(
            "The release assets cannot go to a Gitea release:\n- More than one asset has the file name 'setup.exe'.");
        await Assert.That(harness.Events.Select(static x => x.Name)).DoesNotContain("nuget-push");
        await Assert.That(api.Requests.Count).IsEqualTo(1);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(remoteTip);
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // The uploads get the fallback answer, which refuses them, so the release fails after the tag is pushed.
    [Test]
    public async Task Release_WhenAnUploadFails_DeletesTheDraft_ThenTheRecordOfTheTag()
    {
        var api = Answering(new StubHttpAnswer(HttpStatusCode.RequestEntityTooLarge, """{"message":"quota exceeded"}"""));
        using var harness = new ReleaseHarness(new() { GiteaApi = api });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("quota exceeded");
        var deletions = api.Requests.Where(static x => x.Method == HttpMethod.Delete).Select(static x => x.Url);
        string[] expected = [ReleaseUrl, ApiRepositoryUrl + "/tags/" + ReleasedVersion];
        await Assert.That(deletions).IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(harness.Notices).Contains($"Deleted the draft release on tag {ReleasedVersion}.");
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // A release or a tag record deleted by someone else leaves nothing to undo, so the rollback reports it and goes on.
    [Test]
    public async Task Release_WhenAnUploadFails_AndTheDraftIsGone_ReportsIt_AndDeletesTheRecordOfTheTag()
    {
        var notFound = new StubHttpAnswer(HttpStatusCode.NotFound, """{"message":"Not Found"}""");
        var api = Answering(
            new StubHttpAnswer(HttpStatusCode.RequestEntityTooLarge, """{"message":"quota exceeded"}"""),
            releaseDeletion: notFound,
            tagDeletion: notFound);
        using var harness = new ReleaseHarness(new() { GiteaApi = api });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        _ = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(api.Requests[^1].Url).IsEqualTo(ApiRepositoryUrl + "/tags/" + ReleasedVersion);
        var messages = harness.Reporter.Messages;
        await Assert.That(messages.Select(static x => x.Message)).Contains($"Draft release on tag {ReleasedVersion} not found.");
        await Assert.That(harness.Notices).DoesNotContain($"Deleted the draft release on tag {ReleasedVersion}.");
        var warnings = messages.Where(static x => x.Level == MessageLevel.Warning).Select(static x => x.Message);
        await Assert.That(warnings.Any(static x => x.Contains("rollback", StringComparison.Ordinal))).IsFalse();
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // The repository, the draft, the update, and the deletions have answers. The deletions succeed unless
    // releaseDeletion or tagDeletion names another answer. Every other request, the uploads included, gets the
    // fallback answer.
    private static StubHttpMessageHandler Answering(
        StubHttpAnswer fallback,
        bool canPush = true,
        bool hasReleases = true,
        StubHttpAnswer? releaseDeletion = null,
        StubHttpAnswer? tagDeletion = null)
    {
        var repository = $$"""
            {
              "owner": { "login": "tenacom", "visibility": "public" },
              "private": false,
              "has_releases": {{(hasReleases ? "true" : "false")}},
              "permissions": { "push": {{(canPush ? "true" : "false")}} }
            }
            """;
        return new(
            new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> {
                [(HttpMethod.Get, ApiRepositoryUrl)] = new(HttpStatusCode.OK, repository),
                [(HttpMethod.Post, ApiRepositoryUrl + "/releases")] = new(HttpStatusCode.Created, """{"id":42,"draft":true}"""),
                [(HttpMethod.Patch, ReleaseUrl)] = new(HttpStatusCode.OK, """{"id":42}"""),
                [(HttpMethod.Delete, ReleaseUrl)] = releaseDeletion ?? new(HttpStatusCode.NoContent),
                [(HttpMethod.Delete, ApiRepositoryUrl + "/tags/" + ReleasedVersion)] = tagDeletion ?? new(HttpStatusCode.NoContent),
            },
            fallback);
    }
}
