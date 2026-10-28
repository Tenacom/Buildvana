// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Tool.Services.Hosts.GitLab;
using Buildvana.Tool.Utilities;

// The answers are canned here: what the client is judged on is what it sends and what it makes of the shape
// GitLab documents, not whether the network is up.
internal sealed class GitLabApiClientTests
{
    private const string Project = "tenacom/tools/buildvana";
    private const string Token = "secret-token";
    private const string ApiProjectUrl = "https://gitlab.com/api/v4/projects/tenacom%2Ftools%2Fbuildvana";
    private const string PackageFileUrl = ApiProjectUrl + "/packages/generic/buildvana/1.0.0-preview/Foo.1.0.0.nupkg";

    private static readonly GitLabNewRelease NewRelease = new() {
        TagName = "1.0.0-preview",
        Name = "1.0.0-preview",
        Description = "Text",
        Assets = new GitLabReleaseAssets {
            Links = [
                new GitLabReleaseLink {
                    Name = "Foo package",
                    Url = PackageFileUrl,
                    DirectAssetPath = "/Foo.1.0.0.nupkg",
                    LinkType = "package",
                },
            ],
        },
    };

    [Test]
    public async Task BaseUrl_IsApiV4OfTheHost()
    {
        using var client = new GitLabApiClient("gitlab.example.com", Token, GitLabTokenKind.AccessToken, Answering());
        await Assert.That(client.BaseUrl.AbsoluteUri).IsEqualTo("https://gitlab.example.com/api/v4/");
    }

    [Test]
    [Arguments("", Token)]
    [Arguments("gitlab.com", "")]
    public async Task Constructor_RejectsEmptyArguments(string hostName, string token)
    {
        await Assert.That(() => new GitLabApiClient(hostName, token, GitLabTokenKind.AccessToken, Answering()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_RejectsAnUnknownTokenKind()
    {
        await Assert.That(() => new GitLabApiClient("gitlab.com", Token, (GitLabTokenKind)42, Answering()))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(GitLabTokenKind.AccessToken, "PRIVATE-TOKEN")]
    [Arguments(GitLabTokenKind.JobToken, "JOB-TOKEN")]
    public async Task EveryRequest_CarriesTheApiHeaders(GitLabTokenKind tokenKind, string tokenHeader)
    {
        var handler = Answering((HttpMethod.Get, ApiProjectUrl), """{"visibility":"public"}""");
        using var client = new GitLabApiClient("gitlab.com", Token, tokenKind, handler);
        _ = await client.GetProjectAsync(Project).ConfigureAwait(false);
        var headers = handler.Requests[0].Headers;
        await Assert.That(headers[tokenHeader]).IsEqualTo(Token);
        await Assert.That(headers.Keys).DoesNotContain(tokenKind == GitLabTokenKind.JobToken ? "PRIVATE-TOKEN" : "JOB-TOKEN");
        await Assert.That(headers.Keys).DoesNotContain("Authorization");
        await Assert.That(headers["Accept"]).IsEqualTo("application/json");
        await Assert.That(headers["User-Agent"]).IsEqualTo($"bv/{OwnVersion.Value.ToNormalizedString()}");
    }

    [Test]
    public async Task GetCurrentUserAsync_ReadsTheId()
    {
        var handler = Answering((HttpMethod.Get, "https://gitlab.com/api/v4/user"), """{"id":42,"username":"ric"}""");
        using var client = NewClient(handler);
        var user = await client.GetCurrentUserAsync().ConfigureAwait(false);
        await Assert.That(user.Id).IsEqualTo(42);
        await Assert.That(handler.Requests[0].Headers["PRIVATE-TOKEN"]).IsEqualTo(Token);
    }

    [Test]
    [Arguments("public")]
    [Arguments("internal")]
    [Arguments("private")]
    public async Task GetProjectAsync_ReadsTheVisibility(string visibility)
    {
        var handler = Answering((HttpMethod.Get, ApiProjectUrl), $$"""{"id":7,"visibility":"{{visibility}}"}""");
        using var client = NewClient(handler);
        var project = await client.GetProjectAsync(Project).ConfigureAwait(false);
        await Assert.That(project.Visibility).IsEqualTo(visibility);
        await Assert.That(handler.Requests[0].Body).IsNull();
    }

    [Test]
    public async Task GetProjectAsync_WhenTheAnswerLacksTheVisibility_Fails()
    {
        using var client = NewClient(Answering((HttpMethod.Get, ApiProjectUrl), """{"id":7}"""));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetProjectAsync(Project).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task GetProjectWithoutCredentialsAsync_SendsNoToken_AndReadsTheProject()
    {
        var handler = Answering((HttpMethod.Get, ApiProjectUrl), """{"visibility":"public"}""");
        using var client = new GitLabApiClient("gitlab.com", Token, GitLabTokenKind.JobToken, handler);
        var project = await client.GetProjectWithoutCredentialsAsync(Project).ConfigureAwait(false);
        await Assert.That(project?.Visibility).IsEqualTo("public");
        var headers = handler.Requests[0].Headers;
        await Assert.That(headers.Keys).DoesNotContain("JOB-TOKEN");
        await Assert.That(headers.Keys).DoesNotContain("PRIVATE-TOKEN");
        await Assert.That(headers["User-Agent"]).IsEqualTo($"bv/{OwnVersion.Value.ToNormalizedString()}");
    }

    [Test]
    public async Task GetProjectWithoutCredentialsAsync_WhenTheApiAnswersNotFound_ReturnsNull()
    {
        using var client = NewClient(Answering());
        await Assert.That(await client.GetProjectWithoutCredentialsAsync(Project).ConfigureAwait(false)).IsNull();
    }

    [Test]
    public async Task GetProjectMemberAsync_ReadsTheAccessLevelOfEveryMembership()
    {
        var handler = Answering(
            (HttpMethod.Get, ApiProjectUrl + "/members/all/42"),
            """{"id":42,"username":"ric","access_level":40}""");
        using var client = NewClient(handler);
        var member = await client.GetProjectMemberAsync(Project, 42).ConfigureAwait(false);
        await Assert.That(member).IsEqualTo(new GitLabMember { AccessLevel = 40 });
        await Assert.That(handler.Requests[0].Headers["PRIVATE-TOKEN"]).IsEqualTo(Token);
    }

    [Test]
    public async Task GetProjectMemberAsync_WhenTheApiAnswersNotFound_ReturnsNull()
    {
        using var client = NewClient(Answering());
        await Assert.That(await client.GetProjectMemberAsync(Project, 42).ConfigureAwait(false)).IsNull();
    }

    [Test]
    public async Task GetReleasesAsync_AsksForOnePage_AndReadsTheReleases()
    {
        var handler = Answering(
            (HttpMethod.Get, ApiProjectUrl + "/releases?per_page=1"),
            """[{"tag_name":"0.9.0","name":"0.9.0"}]""");
        using var client = NewClient(handler);
        var releases = await client.GetReleasesAsync(Project, 1).ConfigureAwait(false);
        await Assert.That(releases).IsEquivalentTo([new GitLabRelease { TagName = "0.9.0" }]);
    }

    [Test]
    public async Task GetReleasesAsync_WhenTheProjectHasNoReleases_ReturnsAnEmptyList()
    {
        using var client = NewClient(Answering((HttpMethod.Get, ApiProjectUrl + "/releases?per_page=1"), "[]"));
        await Assert.That(await client.GetReleasesAsync(Project, 1).ConfigureAwait(false)).IsEmpty();
    }

    [Test]
    [Arguments(0)]
    [Arguments(101)]
    public async Task GetReleasesAsync_RejectsACountOutOfRange(int count)
    {
        var handler = Answering();
        using var client = NewClient(handler);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetReleasesAsync(Project, count).ConfigureAwait(false))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task CreateReleaseAsync_PostsTheNewRelease_AndReadsTheRelease()
    {
        var handler = Answering(
            (HttpMethod.Post, ApiProjectUrl + "/releases"),
            """{"tag_name":"1.0.0-preview","name":"1.0.0-preview","description":"Text"}""",
            HttpStatusCode.Created);
        using var client = NewClient(handler);
        var release = await client.CreateReleaseAsync(Project, NewRelease).ConfigureAwait(false);
        await Assert.That(release).IsEqualTo(new GitLabRelease { TagName = "1.0.0-preview" });
        var request = handler.Requests[0];
        await Assert.That(request.Headers["Content-Type"]).IsEqualTo("application/json; charset=utf-8");
        await Assert.That(request.Body).IsEqualTo(
            """{"tag_name":"1.0.0-preview","name":"1.0.0-preview","description":"Text","assets":{"links":["""
            + $$$"""{"name":"Foo package","url":"{{{PackageFileUrl}}}","direct_asset_path":"/Foo.1.0.0.nupkg","link_type":"package"}]}}""");
    }

    [Test]
    public async Task DeleteReleaseAsync_DeletesTheReleaseOfTheTag()
    {
        var handler = Answering((HttpMethod.Delete, ApiProjectUrl + "/releases/release%2F1.0"), """{"tag_name":"release/1.0"}""");
        using var client = NewClient(handler);
        var deleted = await client.DeleteReleaseAsync(Project, "release/1.0").ConfigureAwait(false);
        await Assert.That(deleted).IsTrue();
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DeleteReleaseAsync_WhenTheReleaseDoesNotExist_ReturnsFalse()
    {
        var handler = Answering(
            (HttpMethod.Delete, ApiProjectUrl + "/releases/1.0"),
            """{"message":"404 Not found"}""",
            HttpStatusCode.NotFound);
        using var client = NewClient(handler);
        var deleted = await client.DeleteReleaseAsync(Project, "1.0").ConfigureAwait(false);
        await Assert.That(deleted).IsFalse();
    }

    [Test]
    public async Task UploadPackageFileAsync_PutsTheContents_AndReadsThePackageId()
    {
        var handler = Answering(
            (HttpMethod.Put, PackageFileUrl + "?select=package_file"),
            """{"id":11,"package_id":5,"file_name":"Foo.1.0.0.nupkg","size":14}""");
        using var client = NewClient(handler);
        var file = await UploadAsync(client, "Foo.1.0.0.nupkg").ConfigureAwait(false);
        await Assert.That(file.PackageId).IsEqualTo(5);
        var request = handler.Requests[0];
        await Assert.That(request.Headers["Content-Type"]).IsEqualTo("application/octet-stream");
        await Assert.That(request.Body).IsEqualTo("asset contents");
    }

    [Test]
    public async Task UploadPackageFileAsync_WhenTheFileNameIsInvalid_Fails()
    {
        var handler = Answering();
        using var client = NewClient(handler);

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await UploadAsync(client, "Foo 1.0.0.nupkg").ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task GetPackageFileUrl_EscapesEverySegment()
    {
        using var client = NewClient(Answering());
        var url = client.GetPackageFileUrl(Project, "buildvana", "1.0.0+build.7", "Foo@1.0.0.zip");
        await Assert.That(url.AbsoluteUri)
            .IsEqualTo(ApiProjectUrl + "/packages/generic/buildvana/1.0.0%2Bbuild.7/Foo%401.0.0.zip");
    }

    [Test]
    public async Task DeletePackageAsync_DeletesThePackage()
    {
        var handler = Answering((HttpMethod.Delete, ApiProjectUrl + "/packages/5"), null, HttpStatusCode.NoContent);
        using var client = NewClient(handler);
        await client.DeletePackageAsync(Project, 5).ConfigureAwait(false);
        await Assert.That(handler.Requests).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments("Foo.1.0.0.nupkg", true)]
    [Arguments("Foo_Bar-1.0.0+build~7@x.zip", true)]
    [Arguments("a", true)]
    [Arguments("", false)]
    [Arguments("Foo 1.0.0.nupkg", false)]
    [Arguments("dir/Foo.nupkg", false)]
    [Arguments("Föö.nupkg", false)]
    [Arguments("~Foo.nupkg", false)]
    [Arguments("Foo.nupkg~", false)]
    [Arguments("@Foo.nupkg", false)]
    [Arguments("Foo.nupkg@", false)]
    public async Task IsValidPackageFileName_FollowsTheRuleOfTheRegistry(string fileName, bool expected)
    {
        await Assert.That(GitLabApiClient.IsValidPackageFileName(fileName)).IsEqualTo(expected);
    }

    // The rule wants a character after the slash, then at least one more of the class, then a letter or a digit,
    // so the shortest valid path holds three characters after the slash.
    [Test]
    [Arguments("/Foo.1.0.0.nupkg", true)]
    [Arguments("/bin/Foo_Bar-1.0.zip", true)]
    [Arguments("/abc", true)]
    [Arguments("/ab", false)]
    [Arguments("Foo.nupkg", false)]
    [Arguments("//Foo.nupkg", false)]
    [Arguments("/bin//Foo.nupkg", false)]
    [Arguments("/Foo+build.nupkg", false)]
    [Arguments("/Foo.nupkg.", false)]
    [Arguments("/Föö.nupkg", false)]
    public async Task IsValidDirectAssetPath_FollowsTheRuleOfReleaseLinks(string path, bool expected)
    {
        await Assert.That(GitLabApiClient.IsValidDirectAssetPath(path)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(127, true)]
    [Arguments(128, false)]
    public async Task IsValidDirectAssetPath_AcceptsAtMost128Characters(int nameLength, bool expected)
    {
        await Assert.That(GitLabApiClient.IsValidDirectAssetPath("/" + new string('a', nameLength))).IsEqualTo(expected);
    }

    [Test]
    [Arguments("GetCurrentUser")]
    [Arguments("GetProject")]
    [Arguments("GetProjectWithoutCredentials")]
    [Arguments("GetProjectMember")]
    [Arguments("GetReleases")]
    [Arguments("CreateRelease")]
    [Arguments("DeleteRelease")]
    [Arguments("UploadPackageFile")]
    [Arguments("DeletePackage")]
    public async Task Operation_WhenTheApiRefuses_FailsWithTheStatusAndTheMessage(string operation)
    {
        using var client = NewClient(AnsweringEverything(
            new StubHttpAnswer(HttpStatusCode.Forbidden, """{"message":"403 Forbidden"}""")));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await InvokeAsync(client, operation).ConfigureAwait(false))
            .Throws<BuildFailedException>()
            .WithMessageContaining("403 Forbidden: 403 Forbidden", StringComparison.Ordinal);
    }

    [Test]
    [Arguments("""{"message":"404 Project Not Found"}""", ": 404 Project Not Found")]
    [Arguments("""{"message":["Tag name is missing"]}""", """: ["Tag name is missing"]""")]
    [Arguments("""{"message":{"name":["has already been taken"]}}""", """: {"name":["has already been taken"]}""")]
    [Arguments("""{"error":"file_name is invalid"}""", ": file_name is invalid")]
    [Arguments("""{"message":"","error":"tag_name is missing"}""", ": tag_name is missing")]
    public async Task Operation_WhenTheApiRefuses_QuotesTheMessageWhateverItsShape(string body, string expected)
    {
        using var client = NewClient(AnsweringEverything(new StubHttpAnswer(HttpStatusCode.BadRequest, body)));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetProjectAsync(Project).ConfigureAwait(false))
            .Throws<BuildFailedException>()
            .WithMessageMatching("*with 400 Bad Request" + expected);
    }

    [Test]
    [Arguments("<html>Bad gateway</html>")]
    [Arguments("")]
    [Arguments("{}")]
    [Arguments("null")]
    [Arguments("""["Bad gateway"]""")]
    [Arguments("""{"message":null}""")]
    public async Task Operation_WhenTheErrorAnswerStatesNoMessage_FailsWithTheStatus(string body)
    {
        using var client = NewClient(AnsweringEverything(new StubHttpAnswer(HttpStatusCode.BadGateway, body)));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetProjectAsync(Project).ConfigureAwait(false))
            .Throws<BuildFailedException>()
            .WithMessageMatching("*with 502 Bad Gateway.");
    }

    [Test]
    [Arguments("not json")]
    [Arguments("null")]
    public async Task GetProjectWithoutCredentialsAsync_WhenTheAnswerCannotBeRead_Fails(string body)
    {
        using var client = NewClient(Answering((HttpMethod.Get, ApiProjectUrl), body));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetProjectWithoutCredentialsAsync(Project).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task GetProjectAsync_WhenTheHostCannotBeReached_Fails()
    {
        using var client = NewClient(AnsweringEverything(StubHttpAnswer.Throwing(new HttpRequestException("No such host is known."))));

        // ReSharper disable once AccessToDisposedClosure // the assertion invokes the delegate before returning
        await Assert.That(async () => await client.GetProjectAsync(Project).ConfigureAwait(false))
            .Throws<BuildFailedException>();
    }

    private static GitLabApiClient NewClient(StubHttpMessageHandler handler)
        => new("gitlab.com", Token, GitLabTokenKind.AccessToken, handler);

    private static StubHttpMessageHandler Answering()
        => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>());

    private static StubHttpMessageHandler AnsweringEverything(StubHttpAnswer answer)
        => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>(), answer);

    private static StubHttpMessageHandler Answering(
        (HttpMethod Method, string Url) request,
        string? body,
        HttpStatusCode status = HttpStatusCode.OK)
        => new(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> { [request] = new(status, body) });

    private static Task InvokeAsync(GitLabApiClient client, string operation)
        => operation switch {
            "GetCurrentUser" => client.GetCurrentUserAsync(),
            "GetProject" => client.GetProjectAsync(Project),
            "GetProjectWithoutCredentials" => client.GetProjectWithoutCredentialsAsync(Project),
            "GetProjectMember" => client.GetProjectMemberAsync(Project, 42),
            "GetReleases" => client.GetReleasesAsync(Project, 1),
            "CreateRelease" => client.CreateReleaseAsync(Project, NewRelease),
            "DeleteRelease" => client.DeleteReleaseAsync(Project, "1.0.0-preview"),
            "UploadPackageFile" => UploadAsync(client, "Foo.1.0.0.nupkg"),
            "DeletePackage" => client.DeletePackageAsync(Project, 5),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };

    private static Task<GitLabPackageFile> UploadAsync(GitLabApiClient client, string fileName)
        => client.UploadPackageFileAsync(
            Project,
            "buildvana",
            "1.0.0-preview",
            fileName,
            new MemoryStream("asset contents"u8.ToArray()));
}
