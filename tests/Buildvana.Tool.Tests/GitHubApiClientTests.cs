// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Tool.Services.ServerAdapters.Internal.GitHub;
using Buildvana.Tool.Utilities;

// The answers are canned here: what the client is judged on is what it sends and what it makes of the shape
// GitHub documents, not whether the network is up.
internal sealed class GitHubApiClientTests
{
    private const string Owner = "Tenacom";
    private const string Repo = "Buildvana";
    private const string Token = "secret-token";
    private const string ApiRepositoryUrl = "https://api.github.com/repos/Tenacom/Buildvana";
    private const string UploadUrl = "https://uploads.github.com/repos/Tenacom/Buildvana/releases/42/assets";

    private static readonly GitHubRelease Release = new() { Id = 42, UploadUrl = UploadUrl + "{?name,label}" };

    private static readonly GitHubNewRelease NewRelease = new() {
        TagName = "1.0.0",
        Name = "1.0.0 [provisional]",
        TargetCommitish = "main",
        Prerelease = true,
        Draft = true,
    };

    private static readonly GitHubReleaseUpdate ReleaseUpdate = new() {
        TagName = "1.0.0",
        Name = "1.0.0",
        TargetCommitish = "abc123",
        Body = "Text",
        Prerelease = true,
        Draft = false,
    };

    private static readonly GitHubReleaseNotesRequest NotesRequest = new() { TagName = "1.0.0", TargetCommitish = "abc123" };

    [Test]
    public async Task BaseUrl_ForGitHubCom_IsApiGitHubCom()
    {
        using var client = new GitHubApiClient("github.com", Token, Answering());
        await Assert.That(client.BaseUrl.AbsoluteUri).IsEqualTo("https://api.github.com/");
    }

    [Test]
    public async Task BaseUrl_ForAnotherHost_IsApiV3OfTheHost()
    {
        var handler = Answering((HttpMethod.Get, "https://github.example.com/api/v3/repos/Tenacom/Buildvana"), """{"private":false}""");
        using var client = new GitHubApiClient("github.example.com", Token, handler);
        _ = await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false);
        await Assert.That(client.BaseUrl.AbsoluteUri).IsEqualTo("https://github.example.com/api/v3/");
        await Assert.That(handler.Requests[0].Url).IsEqualTo("https://github.example.com/api/v3/repos/Tenacom/Buildvana");
    }

    [Test]
    [Arguments("", Token)]
    [Arguments("github.com", "")]
    public async Task Constructor_RejectsEmptyArguments(string hostName, string token)
    {
        await Assert.That(() => new GitHubApiClient(hostName, token, Answering())).Throws<ArgumentException>();
    }

    [Test]
    public async Task EveryRequest_CarriesTheApiHeaders()
    {
        var handler = Answering((HttpMethod.Get, ApiRepositoryUrl), """{"private":false}""");
        using var client = NewClient(handler);
        _ = await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false);
        var headers = handler.Requests[0].Headers;
        await Assert.That(headers["Accept"]).IsEqualTo("application/vnd.github+json");
        await Assert.That(headers["X-GitHub-Api-Version"]).IsEqualTo("2022-11-28");
        await Assert.That(headers["Authorization"]).IsEqualTo("Bearer secret-token");
        await Assert.That(headers["User-Agent"]).IsEqualTo($"bv/{OwnVersion.Value.ToNormalizedString()}");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task GetRepositoryAsync_ReadsWhetherTheRepositoryIsPrivate(bool isPrivate)
    {
        var handler = Answering((HttpMethod.Get, ApiRepositoryUrl), isPrivate ? """{"private":true}""" : """{"private":false}""");
        using var client = NewClient(handler);
        var repository = await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false);
        await Assert.That(repository.Private).IsEqualTo(isPrivate);
        await Assert.That(handler.Requests[0].Body).IsNull();
    }

    [Test]
    public async Task CreateReleaseAsync_PostsTheNewRelease_AndReadsTheRelease()
    {
        var handler = Answering(
            (HttpMethod.Post, ApiRepositoryUrl + "/releases"),
            """{"id":42,"upload_url":"https://uploads.github.com/repos/Tenacom/Buildvana/releases/42/assets{?name,label}","draft":true}""",
            HttpStatusCode.Created);
        using var client = NewClient(handler);
        var release = await client.CreateReleaseAsync(Owner, Repo, NewRelease).ConfigureAwait(false);
        await Assert.That(release).IsEqualTo(Release);
        var request = handler.Requests[0];
        await Assert.That(request.Headers["Content-Type"]).IsEqualTo("application/json; charset=utf-8");
        await Assert.That(request.Body)
            .IsEqualTo("""{"tag_name":"1.0.0","name":"1.0.0 [provisional]","target_commitish":"main","prerelease":true,"draft":true}""");
    }

    [Test]
    public async Task CreateReleaseAsync_WhenTheAnswerLacksTheUploadUrl_Fails()
    {
        var handler = Answering((HttpMethod.Post, ApiRepositoryUrl + "/releases"), """{"id":42}""", HttpStatusCode.Created);
        using var client = NewClient(handler);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.CreateReleaseAsync(Owner, Repo, NewRelease).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task GenerateReleaseNotesAsync_PostsTheRequest_AndReadsTheNotes()
    {
        var handler = Answering(
            (HttpMethod.Post, ApiRepositoryUrl + "/releases/generate-notes"),
            """{"name":"1.0.0","body":"## What's Changed"}""");
        using var client = NewClient(handler);
        var notes = await client.GenerateReleaseNotesAsync(Owner, Repo, NotesRequest).ConfigureAwait(false);
        await Assert.That(notes.Body).IsEqualTo("## What's Changed");
        await Assert.That(handler.Requests[0].Body).IsEqualTo("""{"tag_name":"1.0.0","target_commitish":"abc123"}""");
    }

    [Test]
    public async Task UpdateReleaseAsync_PatchesTheRelease()
    {
        var handler = Answering((HttpMethod.Patch, ApiRepositoryUrl + "/releases/42"), """{"id":42}""");
        using var client = NewClient(handler);
        await client.UpdateReleaseAsync(Owner, Repo, 42, ReleaseUpdate).ConfigureAwait(false);
        await Assert.That(handler.Requests[0].Body)
            .IsEqualTo("""{"tag_name":"1.0.0","name":"1.0.0","target_commitish":"abc123","body":"Text","prerelease":true,"draft":false}""");
    }

    [Test]
    public async Task DeleteReleaseAsync_DeletesTheRelease()
    {
        var handler = Answering((HttpMethod.Delete, ApiRepositoryUrl + "/releases/42"), null, HttpStatusCode.NoContent);
        using var client = NewClient(handler);
        await client.DeleteReleaseAsync(Owner, Repo, 42).ConfigureAwait(false);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task TagExistsAsync_WhenTheTagExists_ReturnsTrue()
    {
        var handler = Answering((HttpMethod.Get, ApiRepositoryUrl + "/git/ref/tags/1.0.0"), """{"ref":"refs/tags/1.0.0"}""");
        using var client = NewClient(handler);
        await Assert.That(await client.TagExistsAsync(Owner, Repo, "1.0.0").ConfigureAwait(false)).IsTrue();
    }

    [Test]
    public async Task TagExistsAsync_WhenTheApiAnswersNotFound_ReturnsFalse()
    {
        using var client = NewClient(Answering());
        await Assert.That(await client.TagExistsAsync(Owner, Repo, "1.0.0").ConfigureAwait(false)).IsFalse();
    }

    [Test]
    public async Task TagExistsAsync_KeepsTheSlashesOfATagName()
    {
        var handler = Answering();
        using var client = NewClient(handler);
        _ = await client.TagExistsAsync(Owner, Repo, "release/1.0 beta").ConfigureAwait(false);
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(handler.Requests[0].Url).IsEqualTo(ApiRepositoryUrl + "/git/ref/tags/release/1.0%20beta");
    }

    [Test]
    public async Task DeleteTagAsync_DeletesTheReference()
    {
        var handler = Answering((HttpMethod.Delete, ApiRepositoryUrl + "/git/refs/tags/1.0.0"), null, HttpStatusCode.NoContent);
        using var client = NewClient(handler);
        await client.DeleteTagAsync(Owner, Repo, "1.0.0").ConfigureAwait(false);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task UploadReleaseAssetAsync_PostsTheContentsToTheUploadUrl()
    {
        const string url = UploadUrl + "?name=Foo.1.0.0.nupkg&label=Foo%20package";
        var handler = Answering((HttpMethod.Post, url), """{"id":7}""", HttpStatusCode.Created);
        using var client = NewClient(handler);
        await UploadAsync(client, Release, "application/zip", "Foo package").ConfigureAwait(false);
        var request = handler.Requests[0];
        await Assert.That(request.Headers["Content-Type"]).IsEqualTo("application/zip");
        await Assert.That(request.Body).IsEqualTo("asset contents");
    }

    [Test]
    public async Task UploadReleaseAssetAsync_WithoutALabel_SendsTheNameAlone()
    {
        var handler = Answering((HttpMethod.Post, UploadUrl + "?name=Foo.1.0.0.nupkg"), """{"id":7}""", HttpStatusCode.Created);
        using var client = NewClient(handler);
        await UploadAsync(client, Release, "application/zip", null).ConfigureAwait(false);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task UploadReleaseAssetAsync_WhenTheMimeTypeIsInvalid_Fails()
    {
        var handler = Answering();
        using var client = NewClient(handler);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await UploadAsync(client, Release, "not a MIME type", null).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task UploadReleaseAssetAsync_WhenTheUploadUrlIsNotAbsolute_Fails()
    {
        var handler = Answering();
        using var client = NewClient(handler);
        var release = Release with { UploadUrl = "assets" };

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await UploadAsync(client, release, "application/zip", null).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    [Arguments("GetRepository")]
    [Arguments("CreateRelease")]
    [Arguments("GenerateReleaseNotes")]
    [Arguments("UpdateRelease")]
    [Arguments("DeleteRelease")]
    [Arguments("TagExists")]
    [Arguments("DeleteTag")]
    [Arguments("UploadReleaseAsset")]
    public async Task Operation_WhenTheApiRefuses_FailsWithTheStatusAndTheMessage(string operation)
    {
        using var client = NewClient(AnsweringEverything(
            new StubHttpAnswer(HttpStatusCode.UnprocessableEntity, """{"message":"Validation Failed"}""")));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        var exception = await Assert.That(async () => await InvokeAsync(client, operation).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("422 Unprocessable Entity: Validation Failed");
    }

    [Test]
    [Arguments("<html>Bad gateway</html>")]
    [Arguments("{}")]
    [Arguments("null")]
    public async Task Operation_WhenTheErrorAnswerStatesNoMessage_FailsWithTheStatus(string body)
    {
        using var client = NewClient(AnsweringEverything(new StubHttpAnswer(HttpStatusCode.BadGateway, body)));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        var exception = await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Message).EndsWith("with 502 Bad Gateway.");
    }

    [Test]
    [Arguments("not json")]
    [Arguments("null")]
    public async Task GetRepositoryAsync_WhenTheAnswerCannotBeRead_Fails(string body)
    {
        using var client = NewClient(Answering((HttpMethod.Get, ApiRepositoryUrl), body));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task GetRepositoryAsync_WhenTheHostCannotBeReached_Fails()
    {
        using var client = NewClient(AnsweringEverything(StubHttpAnswer.Throwing(new HttpRequestException("No such host is known."))));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task GetRepositoryAsync_WhenTheRequestTimesOut_Fails()
    {
        using var client = NewClient(AnsweringEverything(StubHttpAnswer.Throwing(new TaskCanceledException("The request timed out."))));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task GetRepositoryAsync_WhenCancelled_IsCancelled()
    {
        using var client = NewClient(Answering((HttpMethod.Get, ApiRepositoryUrl), """{"private":false}"""));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(false);

        // ReSharper disable AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo, cancellation.Token).ConfigureAwait(false))
            .Throws<OperationCanceledException>();

        // ReSharper restore AccessToDisposedClosure
    }

    private static GitHubApiClient NewClient(StubHttpMessageHandler handler) => new("github.com", Token, handler);

    private static StubHttpMessageHandler Answering() => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>());

    private static StubHttpMessageHandler AnsweringEverything(StubHttpAnswer answer)
        => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>(), answer);

    private static StubHttpMessageHandler Answering(
        (HttpMethod Method, string Url) request,
        string? body,
        HttpStatusCode status = HttpStatusCode.OK)
        => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> { [request] = new(status, body) });

    private static Task InvokeAsync(GitHubApiClient client, string operation)
        => operation switch {
            "GetRepository" => client.GetRepositoryAsync(Owner, Repo),
            "CreateRelease" => client.CreateReleaseAsync(Owner, Repo, NewRelease),
            "GenerateReleaseNotes" => client.GenerateReleaseNotesAsync(Owner, Repo, NotesRequest),
            "UpdateRelease" => client.UpdateReleaseAsync(Owner, Repo, 42, ReleaseUpdate),
            "DeleteRelease" => client.DeleteReleaseAsync(Owner, Repo, 42),
            "TagExists" => client.TagExistsAsync(Owner, Repo, "1.0.0"),
            "DeleteTag" => client.DeleteTagAsync(Owner, Repo, "1.0.0"),
            "UploadReleaseAsset" => UploadAsync(client, Release, "application/zip", null),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };

    private static Task UploadAsync(GitHubApiClient client, GitHubRelease release, string contentType, string? label)
        => client.UploadReleaseAssetAsync(
            release,
            "Foo.1.0.0.nupkg",
            contentType,
            label,
            new MemoryStream("asset contents"u8.ToArray()));
}
