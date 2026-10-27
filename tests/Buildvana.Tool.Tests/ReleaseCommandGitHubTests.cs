// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;

/// <summary>
/// End-to-end tests of the <c>release</c> command on a real GitHub host, whose API answers from a table
/// (see <see cref="ReleaseHarnessOptions.GitHubApi"/>).
/// </summary>
/// <remarks>
/// <para>The repository URL of the harness is not <c>github.com</c>, so the host is taken for a GitHub
/// Enterprise Server, with its API under <c>/api/v3</c>.</para>
/// </remarks>
[NotInParallel]
internal sealed class ReleaseCommandGitHubTests
{
    private const string ReleasedVersion = "2.3.2-preview";
    private const string ApiRepositoryUrl = "https://git.example.invalid/api/v3/repos/tenacom/test-repo";
    private const string UploadUrl = "https://uploads.example.invalid/repos/tenacom/test-repo/releases/42/assets";

    [Test]
    public async Task Release_CreatesADraft_UploadsTheAssets_AndPublishesOnTheTag()
    {
        var api = Answering(new StubHttpAnswer(HttpStatusCode.Created, """{"id":7}"""));
        using var harness = new ReleaseHarness(new() { GitHubApi = api });

        var exitCode = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        var requests = api.Requests;
        await Assert.That(requests[0].Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(requests[0].Url).IsEqualTo(ApiRepositoryUrl + "/releases");
        await Assert.That(requests[0].Body).Contains("\"draft\":true");

        // One upload per asset, then the notes, then the update that publishes the draft.
        var uploads = requests.Where(x => x.Url.StartsWith(UploadUrl, StringComparison.Ordinal)).ToArray();
        await Assert.That(uploads.Length).IsEqualTo(5);
        var releaseCommitSha = harness.Repo.GetCommits(2)[1].Sha;
        var notes = requests[^2];
        await Assert.That(notes.Url).IsEqualTo(ApiRepositoryUrl + "/releases/generate-notes");
        await Assert.That(notes.Body).IsEqualTo($$"""{"tag_name":"{{ReleasedVersion}}","target_commitish":"{{releaseCommitSha}}"}""");
        var update = requests[^1];
        await Assert.That(update.Method).IsEqualTo(HttpMethod.Patch);
        await Assert.That(update.Url).IsEqualTo(ApiRepositoryUrl + "/releases/42");
        await Assert.That(update.Body).Contains($"\"tag_name\":\"{ReleasedVersion}\"");
        await Assert.That(update.Body).Contains("\"draft\":false");
        await Assert.That(update.Body).Contains("https://git.example.invalid/tenacom/test-repo/blob/release/CHANGELOG.md");
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsEqualTo(releaseCommitSha);
    }

    // The assets are checked before the push, so a refused asset reaches neither the remote nor the feed.
    [Test]
    public async Task Release_WithTwoAssetsOfTheSameFileName_FailsBeforeThePush()
    {
        var api = Answering(new StubHttpAnswer(HttpStatusCode.Created, """{"id":7}"""));
        using var harness = new ReleaseHarness(new() { GitHubApi = api, WithHook = true });
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

        await Assert.That(Act)
            .Throws<BuildFailedException>()
            .WithMessage(
                "GitHub refuses the release assets:\n- More than one asset has the file name 'setup.exe'.",
                StringComparison.Ordinal);
        await Assert.That(harness.Events.Select(static x => x.Name)).DoesNotContain("nuget-push");
        await Assert.That(api.Requests.Any(static x => x.Url.StartsWith(UploadUrl, StringComparison.Ordinal))).IsFalse();
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(remoteTip);
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // The uploads get the fallback answer, which refuses them, so the release fails after the tag is pushed.
    [Test]
    public async Task Release_WhenAnUploadFails_DeletesTheDraft_AndTheTag()
    {
        var api = Answering(new StubHttpAnswer(HttpStatusCode.UnprocessableEntity, """{"message":"Validation Failed"}"""));
        using var harness = new ReleaseHarness(new() { GitHubApi = api });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        await Assert.That(Act).Throws<BuildFailedException>().WithMessageContaining("Validation Failed", StringComparison.Ordinal);
        var delete = api.Requests[^1];
        await Assert.That(delete.Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(delete.Url).IsEqualTo(ApiRepositoryUrl + "/releases/42");
        await Assert.That(harness.Notices).Contains("Deleted the provisional draft release.");
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // A draft deleted by someone else leaves nothing to undo, so the rollback reports it and goes on.
    [Test]
    public async Task Release_WhenAnUploadFails_AndTheDraftIsGone_ReportsIt_AndDeletesTheTag()
    {
        var api = Answering(
            new StubHttpAnswer(HttpStatusCode.UnprocessableEntity, """{"message":"Validation Failed"}"""),
            new StubHttpAnswer(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));
        using var harness = new ReleaseHarness(new() { GitHubApi = api });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        _ = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(api.Requests[^1].Method).IsEqualTo(HttpMethod.Delete);
        var messages = harness.Reporter.Messages;
        await Assert.That(messages.Select(static x => x.Message)).Contains("Provisional draft release not found.");
        await Assert.That(harness.Notices).DoesNotContain("Deleted the provisional draft release.");
        var warnings = messages.Where(static x => x.Level == MessageLevel.Warning).Select(static x => x.Message);
        await Assert.That(warnings.Any(static x => x.Contains("rollback", StringComparison.Ordinal))).IsFalse();
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // The draft, the notes, the update, and the deletion have answers. The deletion succeeds unless
    // deletion names another answer. Every other request, the uploads included, gets the fallback answer.
    private static StubHttpMessageHandler Answering(StubHttpAnswer fallback, StubHttpAnswer? deletion = null)
        => new(
            new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> {
                [(HttpMethod.Post, ApiRepositoryUrl + "/releases")] = new(
                    HttpStatusCode.Created,
                    $$"""{"id":42,"upload_url":"{{UploadUrl}}{?name,label}","draft":true}"""),
                [(HttpMethod.Post, ApiRepositoryUrl + "/releases/generate-notes")] = new(
                    HttpStatusCode.OK,
                    """{"name":"notes","body":"## What's Changed"}"""),
                [(HttpMethod.Patch, ApiRepositoryUrl + "/releases/42")] = new(HttpStatusCode.OK, """{"id":42}"""),
                [(HttpMethod.Delete, ApiRepositoryUrl + "/releases/42")] = deletion ?? new(HttpStatusCode.NoContent),
            },
            fallback);
}
