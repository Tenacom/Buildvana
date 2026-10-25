// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Tool.Services.Hosts.GitLab;

/// <summary>
/// End-to-end tests of the <c>release</c> command on a real GitLab host, whose API answers from a table
/// (see <see cref="ReleaseHarnessOptions.GitLabApi"/>).
/// </summary>
/// <remarks>
/// <para>The repository URL of the harness is not <c>gitlab.com</c>, so the host stands for a self-managed
/// instance, with its API under <c>/api/v4</c>.</para>
/// </remarks>
[NotInParallel]
internal sealed class ReleaseCommandGitLabTests
{
    private const string ReleasedVersion = "2.3.2-preview";
    private const string ApiUrl = "https://git.example.invalid/api/v4";
    private const string ApiProjectUrl = ApiUrl + "/projects/tenacom%2Ftest-repo";
    private const string ReleasesUrl = ApiProjectUrl + "/releases";
    private const string PackageUrl = ApiProjectUrl + "/packages/generic/test-repo/" + ReleasedVersion;
    private const string MemberUrl = ApiProjectUrl + "/members/all/42";

    [Test]
    public async Task Release_ChecksTheRole_UploadsTheAssets_AndCreatesTheReleaseOnTheTag()
    {
        var api = Answering(accessLevel: 40);
        using var harness = new ReleaseHarness(new() { GitLabApi = api });

        var exitCode = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        var requests = api.Requests;
        await Assert.That(requests[0].Url).IsEqualTo(ReleasesUrl + "?per_page=1");
        await Assert.That(requests[1].Url).IsEqualTo(ApiUrl + "/user");
        await Assert.That(requests[2].Url).IsEqualTo(MemberUrl);

        // One upload per asset, then the release that links them.
        var uploads = requests.Where(static x => x.Method == HttpMethod.Put).ToArray();
        await Assert.That(uploads.Length).IsEqualTo(5);
        await Assert.That(uploads.All(static x => x.Url.StartsWith(PackageUrl + "/", StringComparison.Ordinal))).IsTrue();
        var release = requests[^1];
        await Assert.That(release.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(release.Url).IsEqualTo(ReleasesUrl);
        await Assert.That(release.Body).Contains($"\"tag_name\":\"{ReleasedVersion}\"");
        await Assert.That(release.Body).Contains("https://git.example.invalid/tenacom/test-repo/-/blob/release/CHANGELOG.md");
        await Assert.That(release.Body).Contains("\"direct_asset_path\":\"/setup.exe\"");
        var releaseCommitSha = harness.Repo.GetCommits(2)[1].Sha;
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsEqualTo(releaseCommitSha);
    }

    // The check runs before the release commit, so a refusal leaves nothing to roll back.
    [Test]
    public async Task Release_BelowTheMaintainerRole_FailsBeforeTheReleaseCommit()
    {
        var api = Answering(accessLevel: 30);
        using var harness = new ReleaseHarness(new() { GitLabApi = api });
        var remoteTip = harness.Repo.GetRemoteTipSha();

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("below the Maintainer role");
        await Assert.That(api.Requests.Count).IsEqualTo(3);
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(remoteTip);
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // A job token has no role to check, and pushes as gitlab-ci-token.
    [Test]
    public async Task Release_WithAJobToken_ChecksNoRole_AndPushesAsGitLabCIToken()
    {
        var api = Answering(accessLevel: 40);
        using var harness = new ReleaseHarness(new() { GitLabApi = api, HostTokenEnv = GitLabHost.JobTokenVariable });

        var exitCode = await harness.RunAsync().ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(api.Requests.Select(static x => x.Url)).DoesNotContain(ApiUrl + "/user");
        await Assert.That(api.Requests.All(static x => x.Headers["JOB-TOKEN"] == "test-token")).IsTrue();
        await Assert.That(harness.Reporter.Messages.Select(static x => x.Message))
            .Contains("Fallback Git credentials provided by GitLab (protocol username: 'gitlab-ci-token').");
    }

    // The assets are checked before the push, so a refused asset reaches neither the remote nor the feed.
    [Test]
    public async Task Release_WithAnAssetGitLabRefuses_FailsBeforeThePush()
    {
        var api = Answering(accessLevel: 40);
        using var harness = new ReleaseHarness(new() { GitLabApi = api, WithHook = true });
        var remoteTip = harness.Repo.GetRemoteTipSha();
        var artifactsPath = harness.ArtifactsPath;

        harness.HookBehavior = () => {
            var path = Path.Combine(artifactsPath, "Foo 1.zip");
            File.WriteAllText(path, "fake zip");
            File.WriteAllText(Path.Combine(artifactsPath, "Bad.assets.txt"), $"{path}\tapplication/zip\tBad zip\n");
        };

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("'Foo 1.zip' is not a valid file name in the generic package registry.");
        await Assert.That(harness.Events.Select(static x => x.Name)).DoesNotContain("nuget-push");
        await Assert.That(api.Requests.Any(static x => x.Method == HttpMethod.Put)).IsFalse();
        await Assert.That(harness.Repo.GetRemoteTipSha()).IsEqualTo(remoteTip);
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // The uploads succeed and the release creation fails, so the release fails after the tag is pushed.
    [Test]
    public async Task Release_WhenTheReleaseCannotBeCreated_DeletesThePackage_AndTheTag()
    {
        var api = Answering(accessLevel: 40, release: new(HttpStatusCode.BadRequest, """{"message":"Validation failed"}"""));
        using var harness = new ReleaseHarness(new() { GitLabApi = api });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes Act before the harness is disposed
        Task<int> Act() => harness.RunAsync();

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Validation failed");
        var delete = api.Requests[^1];
        await Assert.That(delete.Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(delete.Url).IsEqualTo(ApiProjectUrl + "/packages/7");
        await Assert.That(harness.Notices).Contains($"Deleted generic package test-repo {ReleasedVersion}.");
        await Assert.That(harness.Repo.GetRemoteTagSha(ReleasedVersion)).IsNull();
    }

    // The releases, the user, the membership, the release, and the deletion of the package have answers. Every
    // other request, the uploads included, gets the fallback answer.
    private static StubHttpMessageHandler Answering(int accessLevel, StubHttpAnswer? release = null)
        => new(
            new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> {
                [(HttpMethod.Get, ReleasesUrl + "?per_page=1")] = new(HttpStatusCode.OK, "[]"),
                [(HttpMethod.Get, ApiUrl + "/user")] = new(HttpStatusCode.OK, """{"id":42}"""),
                [(HttpMethod.Get, MemberUrl)] = new(HttpStatusCode.OK, $$"""{"id":42,"access_level":{{accessLevel}}}"""),
                [(HttpMethod.Post, ReleasesUrl)] = release ?? new(HttpStatusCode.Created, $$"""{"tag_name":"{{ReleasedVersion}}"}"""),
                [(HttpMethod.Delete, ApiProjectUrl + "/packages/7")] = new(HttpStatusCode.NoContent),
            },
            new StubHttpAnswer(HttpStatusCode.OK, """{"package_id":7}"""));
}
