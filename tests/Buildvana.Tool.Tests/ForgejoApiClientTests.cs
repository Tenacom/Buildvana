// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Tool.Services.Hosts.Forgejo;
using Buildvana.Tool.Utilities;

// The answers are canned here: what the client is judged on is what it sends and what it makes of the shape
// Forgejo documents, not whether the network is up.
internal sealed class ForgejoApiClientTests
{
    private const string Owner = "Tenacom";
    private const string Repo = "Buildvana";
    private const string Token = "secret-token";
    private const string ApiRepositoryUrl = "https://forgejo.example.com/api/v1/repos/Tenacom/Buildvana";

    private const string RepositoryJson = """
        {
          "owner": { "login": "Tenacom", "visibility": "limited" },
          "private": false,
          "has_releases": true,
          "permissions": { "admin": false, "push": true, "pull": true }
        }
        """;

    private static readonly ForgejoNewRelease NewRelease = new() {
        TagName = "1.0.0",
        TargetCommitish = "abc123",
        Name = "1.0.0",
        Prerelease = true,
        Draft = true,
    };

    private static readonly ForgejoReleaseUpdate ReleaseUpdate = new() {
        Name = "1.0.0",
        Body = "Text",
        Prerelease = true,
        Draft = false,
    };

    [Test]
    public async Task BaseUrl_IsApiV1OfTheHost()
    {
        using var client = NewClient(Answering());
        await Assert.That(client.BaseUrl.AbsoluteUri).IsEqualTo("https://forgejo.example.com/api/v1/");
    }

    [Test]
    [Arguments("", Token)]
    [Arguments("codeberg.org", "")]
    public async Task Constructor_RejectsEmptyArguments(string hostName, string token)
    {
        await Assert.That(() => new ForgejoApiClient(hostName, token, Answering())).Throws<ArgumentException>();
    }

    [Test]
    public async Task EveryRequest_CarriesTheApiHeaders()
    {
        var handler = Answering((HttpMethod.Get, ApiRepositoryUrl), RepositoryJson);
        using var client = NewClient(handler);
        _ = await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false);
        var headers = handler.Requests[0].Headers;
        await Assert.That(headers["Accept"]).IsEqualTo("application/json");
        await Assert.That(headers["Authorization"]).IsEqualTo("token secret-token");
        await Assert.That(headers["User-Agent"]).IsEqualTo($"bv/{OwnVersion.Value.ToNormalizedString()}");
    }

    [Test]
    public async Task GetRepositoryAsync_ReadsTheRepository()
    {
        var handler = Answering((HttpMethod.Get, ApiRepositoryUrl), RepositoryJson);
        using var client = NewClient(handler);
        var repository = await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false);
        await Assert.That(repository.Owner.Visibility).IsEqualTo("limited");
        await Assert.That(repository.Private).IsFalse();
        await Assert.That(repository.HasReleases).IsTrue();
        await Assert.That(repository.Permissions.Push).IsTrue();
        await Assert.That(handler.Requests[0].Body).IsNull();
    }

    [Test]
    public async Task GetRepositoryAsync_EscapesTheOwnerAndTheName()
    {
        var handler = Answering();
        using var client = NewClient(handler);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetRepositoryAsync("Ten acom", "Build#vana").ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(handler.Requests[0].Url).IsEqualTo("https://forgejo.example.com/api/v1/repos/Ten%20acom/Build%23vana");
    }

    [Test]
    public async Task GetRepositoryAsync_WhenTheAnswerLacksThePermissions_Fails()
    {
        const string body = """{"owner":{"visibility":"public"},"private":false,"has_releases":true}""";
        using var client = NewClient(Answering((HttpMethod.Get, ApiRepositoryUrl), body));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task CreateReleaseAsync_PostsTheNewRelease_AndReadsTheRelease()
    {
        var handler = Answering(
            (HttpMethod.Post, ApiRepositoryUrl + "/releases"),
            """{"id":42,"tag_name":"1.0.0","draft":true}""",
            HttpStatusCode.Created);
        using var client = NewClient(handler);
        var release = await client.CreateReleaseAsync(Owner, Repo, NewRelease).ConfigureAwait(false);
        await Assert.That(release.Id).IsEqualTo(42);
        var request = handler.Requests[0];
        await Assert.That(request.Headers["Content-Type"]).IsEqualTo("application/json; charset=utf-8");
        await Assert.That(request.Body)
            .IsEqualTo("""{"tag_name":"1.0.0","target_commitish":"abc123","name":"1.0.0","prerelease":true,"draft":true}""");
    }

    [Test]
    public async Task UpdateReleaseAsync_PatchesTheRelease()
    {
        var handler = Answering((HttpMethod.Patch, ApiRepositoryUrl + "/releases/42"), """{"id":42}""");
        using var client = NewClient(handler);
        await client.UpdateReleaseAsync(Owner, Repo, 42, ReleaseUpdate).ConfigureAwait(false);
        await Assert.That(handler.Requests[0].Body)
            .IsEqualTo("""{"name":"1.0.0","body":"Text","prerelease":true,"draft":false}""");
    }

    [Test]
    public async Task DeleteReleaseAsync_WhenTheReleaseIsDeleted_ReturnsTrue()
    {
        var handler = Answering((HttpMethod.Delete, ApiRepositoryUrl + "/releases/42"), null, HttpStatusCode.NoContent);
        using var client = NewClient(handler);
        await Assert.That(await client.DeleteReleaseAsync(Owner, Repo, 42).ConfigureAwait(false)).IsTrue();
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DeleteReleaseAsync_WhenTheApiAnswersNotFound_ReturnsFalse()
    {
        var handler = Answering();
        using var client = NewClient(handler);
        await Assert.That(await client.DeleteReleaseAsync(Owner, Repo, 42).ConfigureAwait(false)).IsFalse();
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(handler.Requests[0].Url).IsEqualTo(ApiRepositoryUrl + "/releases/42");
    }

    [Test]
    public async Task DeleteTagAsync_WhenTheTagIsDeleted_ReturnsTrue()
    {
        var handler = Answering((HttpMethod.Delete, ApiRepositoryUrl + "/tags/1.0.0"), null, HttpStatusCode.NoContent);
        using var client = NewClient(handler);
        await Assert.That(await client.DeleteTagAsync(Owner, Repo, "1.0.0").ConfigureAwait(false)).IsTrue();
    }

    [Test]
    public async Task DeleteTagAsync_WhenTheApiAnswersNotFound_ReturnsFalse()
    {
        using var client = NewClient(Answering());
        await Assert.That(await client.DeleteTagAsync(Owner, Repo, "1.0.0").ConfigureAwait(false)).IsFalse();
    }

    [Test]
    public async Task DeleteTagAsync_KeepsTheSlashesOfATagName()
    {
        var handler = Answering();
        using var client = NewClient(handler);
        _ = await client.DeleteTagAsync(Owner, Repo, "release/1.0 beta").ConfigureAwait(false);
        await Assert.That(handler.Requests[0].Method).IsEqualTo(HttpMethod.Delete);
        await Assert.That(handler.Requests[0].Url).IsEqualTo(ApiRepositoryUrl + "/tags/release/1.0%20beta");
    }

    [Test]
    public async Task UploadReleaseAssetAsync_PostsTheContentsAsTheAttachmentField()
    {
        const string url = ApiRepositoryUrl + "/releases/42/assets?name=Foo%2B1.0.0.nupkg";
        const string answer = """{"id":7,"name":"Foo+1.0.0.nupkg","type":"attachment"}""";
        var handler = Answering((HttpMethod.Post, url), answer, HttpStatusCode.Created);
        using var client = NewClient(handler);
        await UploadAsync(client, "Foo+1.0.0.nupkg").ConfigureAwait(false);
        var request = handler.Requests[0];
        await Assert.That(request.Headers["Content-Type"]).StartsWith("multipart/form-data; boundary=");
        await Assert.That(request.Body).Contains("name=attachment");
        await Assert.That(request.Body).Contains("filename=Foo+1.0.0.nupkg");
        await Assert.That(request.Body).Contains("Content-Type: application/octet-stream");
        await Assert.That(request.Body).Contains("asset contents");
    }

    [Test]
    [Arguments("GetRepository")]
    [Arguments("CreateRelease")]
    [Arguments("UpdateRelease")]
    [Arguments("DeleteRelease")]
    [Arguments("DeleteTag")]
    [Arguments("UploadReleaseAsset")]
    public async Task Operation_WhenTheApiRefuses_FailsWithTheStatusAndTheMessage(string operation)
    {
        const string body = """{"message":"release 1.0.0 already exists","url":"https://forgejo.example.com/api/swagger"}""";
        using var client = NewClient(AnsweringEverything(new StubHttpAnswer(HttpStatusCode.Conflict, body)));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        var exception = await Assert.That(async () => await InvokeAsync(client, operation).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("409 Conflict: release 1.0.0 already exists");
    }

    [Test]
    public async Task Operation_WhenTheApiAnswersNotFoundWithErrors_FailsWithTheMessage()
    {
        // A Forgejo 404 holds an "errors" member next to the message.
        const string body = """
            {
              "message": "The target couldn't be found.",
              "url": "https://forgejo.example.com/api/swagger",
              "errors": ["user redirect does not exist"]
            }
            """;
        using var client = NewClient(AnsweringEverything(new StubHttpAnswer(HttpStatusCode.NotFound, body)));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        var exception = await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("404 Not Found: The target couldn't be found.");
    }

    [Test]
    [Arguments("<html>Bad gateway</html>")]
    [Arguments("{}")]
    [Arguments("""{"message":"","url":"https://forgejo.example.com/api/swagger"}""")]
    [Arguments("null")]
    public async Task Operation_WhenTheErrorAnswerStatesNoMessage_FailsWithTheStatus(string body)
    {
        using var client = NewClient(AnsweringEverything(new StubHttpAnswer(HttpStatusCode.BadGateway, body)));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        var exception = await Assert.That(async () => await client.GetRepositoryAsync(Owner, Repo).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.Message).EndsWith("with 502 Bad Gateway.");
    }

    private static ForgejoApiClient NewClient(StubHttpMessageHandler handler) => new("forgejo.example.com", Token, handler);

    private static StubHttpMessageHandler Answering() => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>());

    private static StubHttpMessageHandler AnsweringEverything(StubHttpAnswer answer)
        => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>(), answer);

    private static StubHttpMessageHandler Answering(
        (HttpMethod Method, string Url) request,
        string? body,
        HttpStatusCode status = HttpStatusCode.OK)
        => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> { [request] = new(status, body) });

    private static Task InvokeAsync(ForgejoApiClient client, string operation)
        => operation switch {
            "GetRepository" => client.GetRepositoryAsync(Owner, Repo),
            "CreateRelease" => client.CreateReleaseAsync(Owner, Repo, NewRelease),
            "UpdateRelease" => client.UpdateReleaseAsync(Owner, Repo, 42, ReleaseUpdate),
            "DeleteRelease" => client.DeleteReleaseAsync(Owner, Repo, 42),
            "DeleteTag" => client.DeleteTagAsync(Owner, Repo, "1.0.0"),
            "UploadReleaseAsset" => UploadAsync(client, "Foo.1.0.0.nupkg"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };

    private static Task UploadAsync(ForgejoApiClient client, string fileName)
        => client.UploadReleaseAssetAsync(Owner, Repo, 42, fileName, new MemoryStream("asset contents"u8.ToArray()));
}
