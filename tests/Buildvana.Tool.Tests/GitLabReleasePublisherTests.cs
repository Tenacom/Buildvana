// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Hosts.GitLab;
using Buildvana.Tool.Services.Release;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Assertions.Enums;

// The publisher runs over a real GitLabHost, whose API answers from a table, and a real Git repository, whose
// current branch names the changelog the release description links.
[NotInParallel]
internal sealed class GitLabReleasePublisherTests
{
    private const string TokenVariable = "BV_TEST_GITLAB_PUBLISHER_TOKEN";
    private const string Tag = "1.0.0";
    private const string ApiUrl = "https://gitlab.com/api/v4";
    private const string ApiProjectUrl = ApiUrl + "/projects/tenacom%2Fbuildvana";
    private const string ReleasesUrl = ApiProjectUrl + "/releases";
    private const string PackageUrl = ApiProjectUrl + "/packages/generic/buildvana/" + Tag;
    private const string MemberUrl = ApiProjectUrl + "/members/all/42";

    [Test]
    public async Task BeginAsync_WithAnAccessToken_ReadsTheReleases_ThenTheRoleOfTheUser()
    {
        var api = Answering(Member(40));

        await RunAsync(api, static (publisher, _) => publisher.BeginAsync()).ConfigureAwait(false);

        string[] expected = [ReleasesUrl + "?per_page=1", ApiUrl + "/user", MemberUrl];
        await Assert.That(api.Requests.Select(static x => x.Url)).IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(api.Requests.All(static x => x.Headers["PRIVATE-TOKEN"] == "secret")).IsTrue();
    }

    [Test]
    public async Task BeginAsync_BelowTheMaintainerRole_Throws()
    {
        using var api = Answering(Member(30));

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before api is disposed
        var exception = await Assert.That(() => RunAsync(api, static (publisher, _) => publisher.BeginAsync()))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("access level 30 on tenacom/buildvana, below the Maintainer role");
    }

    // An instance administrator, or an organization owner, has no membership and can still delete a package.
    [Test]
    public async Task BeginAsync_WhenTheUserIsNotAMember_Proceeds()
    {
        var api = Answering();
        var reporter = new CaptureReporter();

        await RunAsync(api, static (publisher, _) => publisher.BeginAsync(), reporter).ConfigureAwait(false);

        await Assert.That(api.Requests[^1].Url).IsEqualTo(MemberUrl);
        await Assert.That(reporter.Messages.Select(static x => x.Message))
            .Contains("Role check skipped: the user of the token is not a member of the project.");
    }

    [Test]
    public async Task BeginAsync_WithAJobToken_ReadsTheReleasesAlone()
    {
        var api = Answering();

        await RunAsync(api, static (publisher, _) => publisher.BeginAsync(), tokenEnv: GitLabHost.JobTokenVariable)
            .ConfigureAwait(false);

        await Assert.That(api.Requests).Count().IsEqualTo(1);
        await Assert.That(api.Requests[0].Url).IsEqualTo(ReleasesUrl + "?per_page=1");
        await Assert.That(api.Requests[0].Headers["JOB-TOKEN"]).IsEqualTo("secret");
    }

    [Test]
    public async Task BeginAsync_WhenTheReleasesCannotBeRead_Throws_AndReadsNothingElse()
    {
        using var api = new StubHttpMessageHandler(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> {
            [(HttpMethod.Get, ReleasesUrl + "?per_page=1")] = new(HttpStatusCode.Forbidden, """{"message":"403 Forbidden"}"""),
        });

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before api is disposed
        var exception = await Assert.That(() => RunAsync(api, static (publisher, _) => publisher.BeginAsync()))
            .Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("403 Forbidden");
        await Assert.That(api.Requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PublishAsync_UploadsEachAsset_ThenCreatesAReleaseThatLinksThem()
    {
        var api = Answering();

        await RunAsync(api, static (publisher, assets) => publisher.PublishAsync(Tag, "abc123", assets)).ConfigureAwait(false);

        var requests = api.Requests;
        await Assert.That(requests).Count().IsEqualTo(3);
        await Assert.That(requests[0].Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(requests[0].Url).IsEqualTo(PackageUrl + "/Foo.1.0.0.nupkg?select=package_file");
        await Assert.That(requests[0].Body).IsEqualTo("fake package");
        await Assert.That(requests[1].Url).IsEqualTo(PackageUrl + "/setup.exe?select=package_file");
        var release = requests[2];
        await Assert.That(release.Method).IsEqualTo(HttpMethod.Post);
        await Assert.That(release.Url).IsEqualTo(ReleasesUrl);
        await Assert.That(release.Body).Contains($"\"tag_name\":\"{Tag}\",\"name\":\"{Tag}\"");
        await Assert.That(release.Body).Contains("(https://gitlab.com/tenacom/buildvana/-/blob/release/CHANGELOG.md)");
        const string packageUrl = PackageUrl + "/Foo.1.0.0.nupkg";
        const string setupUrl = PackageUrl + "/setup.exe";
        await Assert.That(release.Body).Contains(
            $$"""{"name":"Foo.1.0.0.nupkg","url":"{{packageUrl}}","direct_asset_path":"/Foo.1.0.0.nupkg","link_type":"package"}""");
        await Assert.That(release.Body).Contains(
            $$"""{"name":"Setup program","url":"{{setupUrl}}","direct_asset_path":"/setup.exe","link_type":"package"}""");
    }

    [Test]
    public async Task PublishAsync_WithAnAssetGitLabRefuses_UploadsNothing()
    {
        using var api = Answering();

        static Task Publish(ReleasePublisher publisher, IReadOnlyList<ReleaseAsset> assets)
            => publisher.PublishAsync(Tag, "abc123", [.. assets, new("dir/Foo 2.zip", "Bad", "x")]);

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before api is disposed
        var exception = await Assert.That(() => RunAsync(api, Publish)).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("'Foo 2.zip'");
        await Assert.That(api.Requests).IsEmpty();
    }

    // The release goes first, because its links point at the files of the package.
    [Test]
    public async Task UndoAsync_AfterPublishAsync_DeletesTheRelease_ThenThePackage()
    {
        var api = Answering();
        var reporter = new CaptureReporter();

        await RunAsync(
            api,
            static async (publisher, assets) => {
                await publisher.PublishAsync(Tag, "abc123", assets).ConfigureAwait(false);
                await publisher.UndoAsync().ConfigureAwait(false);
                await publisher.UndoAsync().ConfigureAwait(false);
            },
            reporter).ConfigureAwait(false);

        var deletions = api.Requests.Where(static x => x.Method == HttpMethod.Delete).Select(static x => x.Url);
        string[] expected = [ReleasesUrl + "/" + Tag, ApiProjectUrl + "/packages/7"];
        await Assert.That(deletions).IsEquivalentTo(expected, CollectionOrdering.Matching);
        var notices = reporter.Messages.Where(static x => x.Level == MessageLevel.Notice).Select(static x => x.Message);
        string[] expectedNotices = [$"Deleted release {Tag}.", $"Deleted generic package buildvana {Tag}."];
        await Assert.That(notices).IsEquivalentTo(expectedNotices, CollectionOrdering.Matching);
    }

    [Test]
    public async Task UndoAsync_WhenTheReleaseWasNotCreated_DeletesThePackage()
    {
        var api = Answering(release: new(HttpStatusCode.BadRequest, """{"message":{"links":["is invalid"]}}"""));

        await RunAsync(
            api,
            static async (publisher, assets) => {
                await Assert.That(() => publisher.PublishAsync(Tag, "abc123", assets)).Throws<BuildFailedException>();
                await publisher.UndoAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

        var deletions = api.Requests.Where(static x => x.Method == HttpMethod.Delete).Select(static x => x.Url);
        string[] expected = [ApiProjectUrl + "/packages/7"];
        await Assert.That(deletions).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    public async Task UndoAsync_WithNothingPublished_SendsNothing()
    {
        var api = Answering();

        await RunAsync(api, static (publisher, _) => publisher.UndoAsync()).ConfigureAwait(false);

        await Assert.That(api.Requests).IsEmpty();
    }

    [Test]
    public async Task CheckAssets_ReportsEveryAssetGitLabRefuses()
    {
        ReleaseAsset[] assets = [
            new("out/Foo 1.zip", "Foo", "application/zip"),
            new("out/a+b.zip", "Plus", "application/zip"),
            new("one/Bar.zip", "Bar", "application/zip"),
            new("two/Bar.zip", "Bar", "application/zip"),
        ];

        var exception = await Assert.That(() => GitLabReleasePublisher.CheckAssets(assets)).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).IsEqualTo(
            "GitLab refuses the release assets:"
            + "\n- 'Foo 1.zip' is not a valid file name in the generic package registry."
            + "\n- 'a+b.zip' is not a valid file name for a release link."
            + "\n- More than one asset has the file name 'Bar.zip'."
            + "\n- More than one asset has the description 'Bar'.");
    }

    [Test]
    public async Task CheckAssets_AcceptsAssetsGitLabAccepts()
    {
        ReleaseAsset[] assets = [
            new("out/Foo.1.0.0.nupkg", "Foo.1.0.0.nupkg", "application/octet-stream"),
            new("out/setup.exe", "Setup program", "application/vnd.microsoft.portable-executable"),
        ];

        await Assert.That(() => GitLabReleasePublisher.CheckAssets(assets)).ThrowsNothing();
    }

    private static StubHttpAnswer Member(int accessLevel)
        => new(HttpStatusCode.OK, $$"""{"id":42,"access_level":{{accessLevel}}}""");

    // The releases, the user, every upload, and the deletions have answers. The membership has one when
    // member is not null, and the release creation has a 201 unless release names another answer.
    private static StubHttpMessageHandler Answering(StubHttpAnswer? member = null, StubHttpAnswer? release = null)
    {
        var answers = new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> {
            [(HttpMethod.Get, ReleasesUrl + "?per_page=1")] = new(HttpStatusCode.OK, "[]"),
            [(HttpMethod.Get, ApiUrl + "/user")] = new(HttpStatusCode.OK, """{"id":42}"""),
            [(HttpMethod.Put, PackageUrl + "/Foo.1.0.0.nupkg?select=package_file")] = new(HttpStatusCode.OK, """{"package_id":7}"""),
            [(HttpMethod.Put, PackageUrl + "/setup.exe?select=package_file")] = new(HttpStatusCode.OK, """{"package_id":7}"""),
            [(HttpMethod.Post, ReleasesUrl)] = release ?? new(HttpStatusCode.Created, $$"""{"tag_name":"{{Tag}}"}"""),
            [(HttpMethod.Delete, ReleasesUrl + "/" + Tag)] = new(HttpStatusCode.OK, $$"""{"tag_name":"{{Tag}}"}"""),
            [(HttpMethod.Delete, ApiProjectUrl + "/packages/7")] = new(HttpStatusCode.NoContent),
        };
        if (member is not null)
        {
            answers[(HttpMethod.Get, MemberUrl)] = member;
        }

        return new StubHttpMessageHandler(answers);
    }

    private static async Task RunAsync(
        StubHttpMessageHandler api,
        Func<ReleasePublisher, IReadOnlyList<ReleaseAsset>, Task> act,
        CaptureReporter? reporter = null,
        string tokenEnv = TokenVariable)
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("Foo.1.0.0.nupkg", "fake package");
        repo.WriteFile("setup.exe", "fake setup");
        repo.CommitAll();
        repo.CheckoutNewBranch("release");
        ReleaseAsset[] assets = [
            new(Path.Combine(repo.RootPath, "Foo.1.0.0.nupkg"), "Foo.1.0.0.nupkg", "application/octet-stream"),
            new(Path.Combine(repo.RootPath, "setup.exe"), "Setup program", "application/vnd.microsoft.portable-executable"),
        ];

        reporter ??= new CaptureReporter();
        using var git = new GitService(reporter, new FixedHomeDirectoryProvider(repo.RootPath));
        var services = new ServiceCollection()
            .AddSingleton(new BuildvanaConfig { Hosts = new() { GitLab = new() { TokenEnv = tokenEnv } } })
            .AddSingleton<IReporter>(reporter)
            .AddSingleton<IGitService>(git)
            .BuildServiceProvider();
        await using (services.ConfigureAwait(false))
        {
            using var host = new GitLabHost(services, Parse("https://gitlab.com/tenacom/buildvana"), api);
            var originalToken = Environment.GetEnvironmentVariable(tokenEnv);
            Environment.SetEnvironmentVariable(tokenEnv, "secret");
            try
            {
                await act(host.CreateReleasePublisher(), assets).ConfigureAwait(false);
            }
            finally
            {
                Environment.SetEnvironmentVariable(tokenEnv, originalToken);
            }
        }
    }

    private static GitUrlInfo Parse(string url) => GitUrlInfo.TryCreate(url, out var info) ? info : throw new InvalidOperationException(url);
}
