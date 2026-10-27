// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;

internal sealed class UrlMatchingReleaseRemoteResolverTests
{
    private const string RepositoryUrl = "https://github.com/Tenacom/Buildvana";

    // The scheme, the user, the case, and a .git suffix play no part in the match.
    [Test]
    [Arguments("https://github.com/Tenacom/Buildvana.git")]
    [Arguments("https://someone@GitHub.com/tenacom/buildvana/")]
    [Arguments("http://github.com/Tenacom/Buildvana")]
    public async Task Resolve_FindsTheRemoteThatNamesTheRepository(string upstreamUrl)
    {
        using var repo = CreateRepo(("origin", "https://github.com/someone/Buildvana"), ("upstream", upstreamUrl));

        await Assert.That(Resolve(repo, RepositoryUrl)).IsEqualTo("upstream");
    }

    [Test]
    public async Task Resolve_PrefersAnHttpsRemote_OverAnSshRemoteOfTheSameRepository()
    {
        using var repo = CreateRepo(("ssh", "ssh://git@github.com/Tenacom/Buildvana.git"), ("https", RepositoryUrl));

        await Assert.That(Resolve(repo, RepositoryUrl)).IsEqualTo("https");
    }

    [Test]
    public async Task Resolve_WithAnSshRemoteAlone_Throws()
    {
        using var repo = CreateRepo(("origin", "ssh://git@github.com/Tenacom/Buildvana.git"));

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before repo is disposed
        var exception = Assert.Throws<BuildFailedException>(() => Resolve(repo, RepositoryUrl));
        await Assert.That(exception.Message).Contains("Remote 'origin' names repository.url");
        await Assert.That(exception.Message).Contains("HTTP and HTTPS");
    }

    [Test]
    public async Task Resolve_WithoutAMatchingRemote_ThrowsNamingTheRemotes()
    {
        using var repo = CreateRepo(("origin", "https://github.com/someone/Buildvana"));

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before repo is disposed
        var exception = Assert.Throws<BuildFailedException>(() => Resolve(repo, RepositoryUrl));
        await Assert.That(exception.Message).Contains($"No remote of the clone names repository.url '{RepositoryUrl}'");
        await Assert.That(exception.Message).Contains("'origin' (https://github.com/someone/Buildvana)");
    }

    [Test]
    public async Task Resolve_WithoutRemotes_Throws()
    {
        using var repo = CreateRepo();

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before repo is disposed
        await Assert.That(() => Resolve(repo, RepositoryUrl))
            .Throws<BuildFailedException>()
            .WithMessageContaining("The clone has no remotes.", StringComparison.Ordinal);
    }

    [Test]
    public async Task Resolve_WithoutRepositoryUrl_Throws()
    {
        using var repo = CreateRepo(("origin", RepositoryUrl));

        // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before repo is disposed
        await Assert.That(() => Resolve(repo, null))
            .Throws<BuildFailedException>()
            .WithMessageContaining("repository.url", StringComparison.Ordinal);
    }

    private static TempGitRepo CreateRepo(params (string Name, string Url)[] remotes)
    {
        var repo = new TempGitRepo();
        repo.WriteFile("a.txt", "content");
        repo.CommitAll();
        foreach (var (name, url) in remotes)
        {
            repo.AddRemote(name, new Uri(url));
        }

        return repo;
    }

    private static string Resolve(TempGitRepo repo, string? repositoryUrl)
    {
        using var git = new GitService(NullReporter.Instance, new FixedHomeDirectoryProvider(repo.RootPath));
        var config = new BuildvanaConfig { Repository = new() { Url = repositoryUrl } };
        return new UrlMatchingReleaseRemoteResolver(NullReporter.Instance, git, config).Resolve();
    }
}
