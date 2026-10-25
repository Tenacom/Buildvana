// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Hosts.Gitea;
using Microsoft.Extensions.DependencyInjection;

internal sealed class GiteaHostTests
{
    private const string TokenVariable = "BV_TEST_GITEA_HOST_TOKEN";

    [Test]
    public async Task Members_DescribeTheRepository()
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            using var host = new GiteaHost(services, Parse("git@gitea.com:tenacom/buildvana.git"));

            await Assert.That(host.Name).IsEqualTo("Gitea");
            await Assert.That(host.Kind).IsEqualTo(HostKind.Gitea);
            await Assert.That(host.RepositoryUrl.AbsoluteUri).IsEqualTo("https://gitea.com/tenacom/buildvana");
            await Assert.That(host.TokenEnv).IsEqualTo(TokenVariable);
            await Assert.That(host.PushUsername).IsEqualTo("token");
            await Assert.That(host.GetReleaseUrl("1.0.0").AbsoluteUri).IsEqualTo("https://gitea.com/tenacom/buildvana/releases/tag/1.0.0");
        }
    }

    // Gitea names the kind of the reference in a file URL, so a tag and a branch of the same name differ.
    [Test]
    [Arguments(GitRefKind.Tag, "tag")]
    [Arguments(GitRefKind.Branch, "branch")]
    [Arguments(GitRefKind.Commit, "commit")]
    public async Task GetFileUrl_NamesTheKindOfTheReference(GitRefKind kind, string expectedSegment)
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            using var host = new GiteaHost(services, Parse("https://gitea.com/tenacom/buildvana"));
            var gitRef = kind switch {
                GitRefKind.Tag => GitRef.Tag("v1"),
                GitRefKind.Branch => GitRef.Branch("v1"),
                _ => GitRef.Commit("v1"),
            };

            var url = host.GetFileUrl(@"docs\README.md", gitRef);

            await Assert.That(url.AbsoluteUri).IsEqualTo($"https://gitea.com/tenacom/buildvana/src/{expectedSegment}/v1/docs/README.md");
        }
    }

    [Test]
    [Arguments("../outside.md")]
    [Arguments("/docs/README.md")]
    public async Task GetFileUrl_ForAPathOutsideTheRepository_Throws(string path)
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            using var host = new GiteaHost(services, Parse("https://gitea.com/tenacom/buildvana"));

            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before host is disposed
            await Assert.That(() => host.GetFileUrl(path, GitRef.Branch("main"))).Throws<ArgumentException>();
        }
    }

    [Test]
    public async Task Constructor_ForAPathOfThreeSegments_Throws()
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion runs the lambda before services is disposed
            var exception = await Assert.That(() => new GiteaHost(services, Parse("https://gitea.com/a/b/c")))
                .Throws<BuildFailedException>();
            await Assert.That(exception!.Message).Contains("not a valid Gitea repository URL");
        }
    }

    // Anonymous users cannot see the repositories of an owner of limited or private visibility.
    [Test]
    [NotInParallel]
    [Arguments(true, "public", true)]
    [Arguments(false, "limited", true)]
    [Arguments(false, "private", true)]
    [Arguments(false, "public", false)]
    public async Task IsPrivateRepositoryAsync_ReadsThePrivacyOfTheRepository_AndTheVisibilityOfItsOwner(
        bool isPrivate,
        string ownerVisibility,
        bool expected)
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            var api = new StubHttpMessageHandler(new Dictionary<string, string> {
                ["https://gitea.com/api/v1/repos/tenacom/buildvana"] = Repository(isPrivate, ownerVisibility),
            });
            using var host = new GiteaHost(services, Parse("https://gitea.com/tenacom/buildvana"), api);
            Environment.SetEnvironmentVariable(TokenVariable, "secret");
            try
            {
                await Assert.That(await host.IsPrivateRepositoryAsync().ConfigureAwait(false)).IsEqualTo(expected);
                await Assert.That(api.Requests[0].Headers["Authorization"]).IsEqualTo("token secret");
            }
            finally
            {
                Environment.SetEnvironmentVariable(TokenVariable, null);
            }
        }
    }

    // The client is created on first use, so a missing token fails the call that needs the API, and nothing before it.
    [Test]
    [NotInParallel]
    public async Task IsPrivateRepositoryAsync_WithoutAToken_Throws()
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            using var api = new StubHttpMessageHandler(new Dictionary<(HttpMethod Method, string Url), StubHttpAnswer>());
            using var host = new GiteaHost(services, Parse("https://gitea.com/tenacom/buildvana"), api);
            Environment.SetEnvironmentVariable(TokenVariable, null);

            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before host is disposed
            var exception = await Assert.That(host.IsPrivateRepositoryAsync).Throws<BuildFailedException>();
            await Assert.That(exception!.Message).Contains(TokenVariable);
            await Assert.That(api.Requests).IsEmpty();
        }
    }

    private static string Repository(bool isPrivate, string ownerVisibility)
        => $$"""
            {
              "owner": { "login": "tenacom", "visibility": "{{ownerVisibility}}" },
              "private": {{(isPrivate ? "true" : "false")}},
              "has_releases": true,
              "permissions": { "push": true }
            }
            """;

    private static GitUrlInfo Parse(string url)
        => GitUrlInfo.TryCreate(url, out var info) ? info : throw new InvalidOperationException(url);

    private static ServiceProvider Services()
        => new ServiceCollection()
            .AddSingleton(new BuildvanaConfig { Hosts = new() { Gitea = new() { TokenEnv = TokenVariable } } })
            .AddSingleton<IReporter>(NullReporter.Instance)
            .BuildServiceProvider();
}
