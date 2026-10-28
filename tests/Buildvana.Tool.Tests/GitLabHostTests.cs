// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Hosts.GitLab;
using Microsoft.Extensions.DependencyInjection;

internal sealed class GitLabHostTests
{
    private const string TokenVariable = "BV_TEST_GITLAB_HOST_TOKEN";
    private const string ApiProjectUrl = "https://gitlab.com/api/v4/projects/tenacom%2Ftools%2Fbuildvana";

    [Test]
    public async Task Members_DescribeTheProject()
    {
        var services = Services(TokenVariable);
        await using (services.ConfigureAwait(false))
        {
            using var host = new GitLabHost(services, Parse("git@gitlab.com:tenacom/tools/buildvana.git"));

            await Assert.That(host.Name).IsEqualTo("GitLab");
            await Assert.That(host.Kind).IsEqualTo(HostKind.GitLab);
            await Assert.That(host.ProjectPath).IsEqualTo("tenacom/tools/buildvana");
            await Assert.That(host.RepositoryUrl.AbsoluteUri).IsEqualTo("https://gitlab.com/tenacom/tools/buildvana");
            await Assert.That(host.TokenEnv).IsEqualTo(TokenVariable);
            await Assert.That(host.GetReleaseUrl("1.0.0").AbsoluteUri)
                .IsEqualTo("https://gitlab.com/tenacom/tools/buildvana/-/releases/1.0.0");
        }
    }

    // The name of the variable is the one way to tell a job token from an access token.
    [Test]
    [Arguments(TokenVariable, GitLabTokenKind.AccessToken, "oauth2")]
    [Arguments("GITLAB_TOKEN", GitLabTokenKind.AccessToken, "oauth2")]
    [Arguments("CI_JOB_TOKEN", GitLabTokenKind.JobToken, "gitlab-ci-token")]
    [Arguments("ci_job_token", GitLabTokenKind.AccessToken, "oauth2")]
    public async Task TokenKind_AndPushUsername_FollowTheNameOfTheVariable(
        string tokenEnv,
        GitLabTokenKind expectedKind,
        string expectedUsername)
    {
        var services = Services(tokenEnv);
        await using (services.ConfigureAwait(false))
        {
            using var host = new GitLabHost(services, Parse("https://gitlab.com/tenacom/buildvana"));

            await Assert.That(host.TokenKind).IsEqualTo(expectedKind);
            await Assert.That(host.PushUsername).IsEqualTo(expectedUsername);
        }
    }

    // GitLab puts a tag, a branch, and a commit in the same place of a file URL.
    [Test]
    [Arguments(GitRefKind.Tag)]
    [Arguments(GitRefKind.Branch)]
    [Arguments(GitRefKind.Commit)]
    public async Task GetFileUrl_TakesTheNameOfAnyKindOfReference(GitRefKind kind)
    {
        var services = Services(TokenVariable);
        await using (services.ConfigureAwait(false))
        {
            using var host = new GitLabHost(services, Parse("https://gitlab.com/tenacom/tools/buildvana"));
            var gitRef = kind switch {
                GitRefKind.Tag => GitRef.Tag("v1"),
                GitRefKind.Branch => GitRef.Branch("v1"),
                _ => GitRef.Commit("v1"),
            };

            var url = host.GetFileUrl(@"docs\README.md", gitRef);

            await Assert.That(url.AbsoluteUri).IsEqualTo("https://gitlab.com/tenacom/tools/buildvana/-/blob/v1/docs/README.md");
        }
    }

    [Test]
    [Arguments("../outside.md")]
    [Arguments("/docs/README.md")]
    public async Task GetFileUrl_ForAPathOutsideTheRepository_Throws(string path)
    {
        var services = Services(TokenVariable);
        await using (services.ConfigureAwait(false))
        {
            using var host = new GitLabHost(services, Parse("https://gitlab.com/tenacom/buildvana"));

            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before host is disposed
            await Assert.That(() => host.GetFileUrl(path, GitRef.Branch("main"))).Throws<ArgumentException>();
        }
    }

    // Only users of the instance can see an internal project, so it counts as private.
    [Test]
    [NotInParallel]
    [Arguments("private", true)]
    [Arguments("internal", true)]
    [Arguments("public", false)]
    public async Task IsPrivateRepositoryAsync_WithAnAccessToken_ReadsTheVisibility(string visibility, bool expected)
    {
        var services = Services(TokenVariable);
        await using (services.ConfigureAwait(false))
        {
            var api = new StubHttpMessageHandler(new Dictionary<string, string> {
                [ApiProjectUrl] = $$"""{"visibility":"{{visibility}}"}""",
            });
            using var host = new GitLabHost(services, Parse("https://gitlab.com/tenacom/tools/buildvana"), api);
            Environment.SetEnvironmentVariable(TokenVariable, "secret");
            try
            {
                await Assert.That(await host.IsPrivateRepositoryAsync().ConfigureAwait(false)).IsEqualTo(expected);
                await Assert.That(api.Requests[0].Headers["PRIVATE-TOKEN"]).IsEqualTo("secret");
            }
            finally
            {
                Environment.SetEnvironmentVariable(TokenVariable, null);
            }
        }
    }

    // A job token cannot read a project, so the request carries no credentials, and GitLab answers 404 for
    // every project that is not public.
    [Test]
    [NotInParallel]
    [Arguments(HttpStatusCode.OK, false)]
    [Arguments(HttpStatusCode.NotFound, true)]
    public async Task IsPrivateRepositoryAsync_WithAJobToken_ReadsTheProjectWithoutCredentials(
        HttpStatusCode status,
        bool expected)
    {
        var services = Services(GitLabHost.JobTokenVariable);
        await using (services.ConfigureAwait(false))
        {
            var api = new StubHttpMessageHandler(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer> {
                [(HttpMethod.Get, ApiProjectUrl)] = new(status, """{"visibility":"public"}"""),
            });
            using var host = new GitLabHost(services, Parse("https://gitlab.com/tenacom/tools/buildvana"), api);
            var originalToken = Environment.GetEnvironmentVariable(GitLabHost.JobTokenVariable);
            Environment.SetEnvironmentVariable(GitLabHost.JobTokenVariable, "job-secret");
            try
            {
                await Assert.That(await host.IsPrivateRepositoryAsync().ConfigureAwait(false)).IsEqualTo(expected);
                var headers = api.Requests[0].Headers;
                await Assert.That(headers.Keys).DoesNotContain("JOB-TOKEN");
                await Assert.That(headers.Keys).DoesNotContain("PRIVATE-TOKEN");
            }
            finally
            {
                Environment.SetEnvironmentVariable(GitLabHost.JobTokenVariable, originalToken);
            }
        }
    }

    // The client is created on first use, so a missing token fails the call that needs the API, and nothing before it.
    [Test]
    [NotInParallel]
    public async Task IsPrivateRepositoryAsync_WithoutAToken_Throws()
    {
        var services = Services(TokenVariable);
        await using (services.ConfigureAwait(false))
        {
            using var api = new StubHttpMessageHandler(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>());
            using var host = new GitLabHost(services, Parse("https://gitlab.com/tenacom/buildvana"), api);
            Environment.SetEnvironmentVariable(TokenVariable, null);

            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before host is disposed
            await Assert.That(host.IsPrivateRepositoryAsync)
                .Throws<BuildFailedException>()
                .WithMessageContaining(TokenVariable, StringComparison.Ordinal);
            await Assert.That(api.Requests).IsEmpty();
        }
    }

    private static GitUrlInfo Parse(string url)
        => GitUrlInfo.TryCreate(url, out var info) ? info : throw new InvalidOperationException(url);

    private static ServiceProvider Services(string tokenEnv)
        => new ServiceCollection()
            .AddSingleton(new BuildvanaConfig { Hosts = new() { GitLab = new() { TokenEnv = tokenEnv } } })
            .AddSingleton<IReporter>(NullReporter.Instance)
            .BuildServiceProvider();
}
