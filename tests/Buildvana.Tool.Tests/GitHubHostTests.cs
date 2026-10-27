// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Hosts.GitHub;
using Microsoft.Extensions.DependencyInjection;

internal sealed class GitHubHostTests
{
    private const string TokenVariable = "BV_TEST_GITHUB_HOST_TOKEN";

    [Test]
    public async Task Members_DescribeTheRepository()
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            using var host = new GitHubHost(services, Parse("git@github.com:Tenacom/Buildvana.git"));

            await Assert.That(host.Name).IsEqualTo("GitHub");
            await Assert.That(host.Kind).IsEqualTo(HostKind.GitHub);
            await Assert.That(host.RepositoryUrl.AbsoluteUri).IsEqualTo("https://github.com/Tenacom/Buildvana");
            await Assert.That(host.TokenEnv).IsEqualTo(TokenVariable);
            await Assert.That(host.PushUsername).IsEqualTo("x-access-token");
            await Assert.That(host.GetReleaseUrl("1.0.0").AbsoluteUri).IsEqualTo("https://github.com/Tenacom/Buildvana/releases/tag/1.0.0");
        }
    }

    // GitHub puts a tag, a branch, and a commit in the same place of a file URL.
    [Test]
    [Arguments(GitRefKind.Tag)]
    [Arguments(GitRefKind.Branch)]
    [Arguments(GitRefKind.Commit)]
    public async Task GetFileUrl_TakesTheNameOfAnyKindOfReference(GitRefKind kind)
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            using var host = new GitHubHost(services, Parse("https://github.com/Tenacom/Buildvana"));
            var gitRef = kind switch {
                GitRefKind.Tag => GitRef.Tag("v1"),
                GitRefKind.Branch => GitRef.Branch("v1"),
                _ => GitRef.Commit("v1"),
            };

            var url = host.GetFileUrl("docs/README.md", gitRef);

            await Assert.That(url.AbsoluteUri).IsEqualTo("https://github.com/Tenacom/Buildvana/blob/v1/docs/README.md");
        }
    }

    [Test]
    public async Task Constructor_ForAPathOfThreeSegments_Throws()
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before services is disposed
            await Assert.That(() => new GitHubHost(services, Parse("https://github.com/a/b/c")))
                .Throws<BuildFailedException>()
                .WithMessageContaining("not a valid GitHub repository URL", StringComparison.Ordinal);
        }
    }

    [Test]
    [NotInParallel]
    public async Task IsPrivateRepositoryAsync_AsksTheApi()
    {
        var services = Services();
        await using (services.ConfigureAwait(false))
        {
            var api = new StubHttpMessageHandler(new Dictionary<string, string> {
                ["https://api.github.com/repos/Tenacom/Buildvana"] = """{"private":true}""",
            });
            using var host = new GitHubHost(services, Parse("https://github.com/Tenacom/Buildvana"), api);
            Environment.SetEnvironmentVariable(TokenVariable, "secret");
            try
            {
                await Assert.That(await host.IsPrivateRepositoryAsync().ConfigureAwait(false)).IsTrue();
                await Assert.That(api.Requests[0].Headers["Authorization"]).IsEqualTo("Bearer secret");
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
            using var host = new GitHubHost(services, Parse("https://github.com/Tenacom/Buildvana"), api);
            Environment.SetEnvironmentVariable(TokenVariable, null);

            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before host is disposed
            await Assert.That(host.IsPrivateRepositoryAsync)
                .Throws<BuildFailedException>()
                .WithMessageContaining(TokenVariable, StringComparison.Ordinal);
            await Assert.That(api.Requests).IsEmpty();
        }
    }

    private static GitUrlInfo Parse(string url) => GitUrlInfo.TryCreate(url, out var info) ? info : throw new InvalidOperationException(url);

    private static ServiceProvider Services()
        => new ServiceCollection()
            .AddSingleton(new BuildvanaConfig { Hosts = new() { GitHub = new() { TokenEnv = TokenVariable } } })
            .AddSingleton<IReporter>(NullReporter.Instance)
            .BuildServiceProvider();
}
