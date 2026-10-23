// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.Hosts;
using Buildvana.Tool.Services.Hosts.GitHub;
using Buildvana.Tool.Services.Platforms;
using Microsoft.Extensions.DependencyInjection;

internal sealed class GitHostTests
{
    [Test]
    [Arguments("https://github.com/Tenacom/Buildvana")]
    [Arguments("https://GitHub.com/Tenacom/Buildvana.git")]
    [Arguments("git@github.com:Tenacom/Buildvana.git")]
    public async Task DetectKind_ForGitHubCom_IsGitHub(string url)
    {
        await Assert.That(GitHost.DetectKind(Repository(url), Parse(url), new FakeCIPlatform())).IsEqualTo(HostKind.GitHub);
    }

    [Test]
    public async Task DetectKind_WhenTheConfigurationStatesTheHost_IsThatHost()
    {
        const string url = "https://git.example.invalid/tenacom/test-repo";
        var repository = Repository(url) with { Host = HostKind.GitHub };

        await Assert.That(GitHost.DetectKind(repository, Parse(url), new FakeCIPlatform())).IsEqualTo(HostKind.GitHub);
    }

    // A GitHub Enterprise Server is recognized on its own runners, which name it in GITHUB_SERVER_URL.
    [Test]
    [Arguments("https://git.example.invalid", HostKind.GitHub)]
    [Arguments("https://GIT.example.invalid/", HostKind.GitHub)]
    [Arguments("https://elsewhere.example.invalid", null)]
    public async Task DetectKind_OnAPlatformWithAServer_IsThePlatformKind_WhenTheHostNamesMatch(string serverUrl, HostKind? expected)
    {
        const string url = "https://git.example.invalid/tenacom/test-repo";
        var platform = new FakeCIPlatform { Server = new Uri(serverUrl), Kind = HostKind.GitHub };

        await Assert.That(GitHost.DetectKind(Repository(url), Parse(url), platform)).IsEqualTo(expected);
    }

    [Test]
    public async Task DetectKind_ForAnotherHost_IsNull()
    {
        const string url = "https://git.example.invalid/tenacom/test-repo";

        await Assert.That(GitHost.DetectKind(Repository(url), Parse(url), new FakeCIPlatform())).IsNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task ParseRepositoryUrl_WithoutAUrl_Throws(string? url)
    {
        var exception = await Assert.That(() => GitHost.ParseRepositoryUrl(Repository(url))).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("repository.url");
    }

    [Test]
    public async Task ParseRepositoryUrl_WithAnInvalidUrl_Throws()
    {
        var exception = await Assert.That(() => GitHost.ParseRepositoryUrl(Repository("not a URL"))).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("not a Git URL");
    }

    [Test]
    public async Task Create_ForGitHubCom_CreatesAGitHubHost()
    {
        var services = Services("https://github.com/Tenacom/Buildvana");
        await using (services.ConfigureAwait(false))
        {
            await Assert.That(GitHost.Create(services)).IsTypeOf<GitHubHost>();
        }
    }

    [Test]
    public async Task Create_ForAnotherHost_CreatesAnUnknownHost()
    {
        var services = Services("https://git.example.invalid/group/subgroup/test-repo");
        await using (services.ConfigureAwait(false))
        {
            var host = GitHost.Create(services);

            await Assert.That(host).IsTypeOf<UnknownHost>();
            await Assert.That(host.Kind).IsNull();
            await Assert.That(host.HostName).IsEqualTo("git.example.invalid");
            await Assert.That(host.Owner).IsEqualTo("group/subgroup");
            await Assert.That(host.Repository).IsEqualTo("test-repo");
        }
    }

    [Test]
    public async Task Create_ForAUrlWithOneSegment_Throws()
    {
        var services = Services("https://git.example.invalid/test-repo");
        await using (services.ConfigureAwait(false))
        {
            // ReSharper disable once AccessToDisposedClosure // False positive: the assertion invokes the lambda before services is disposed
            var exception = await Assert.That(() => GitHost.Create(services)).Throws<BuildFailedException>();
            await Assert.That(exception!.Message).Contains("names no repository");
        }
    }

    [Test]
    public async Task UnknownHost_HasNoUrls_NoToken_AndAPublisherThatPublishesNothing()
    {
        var reporter = new CaptureReporter();
        var host = new UnknownHost(Parse("https://git.example.invalid/tenacom/test-repo"), reporter);

        await Assert.That(host.RepositoryUrl).IsNull();
        await Assert.That(host.GetReleaseUrl("1.0.0")).IsNull();
        await Assert.That(host.GetFileUrl("CHANGELOG.md", GitRef.Tag("1.0.0"))).IsNull();
        await Assert.That(host.TokenEnv).IsNull();
        await Assert.That(host.GetToken()).IsNull();
        await Assert.That(host.PushUsername).IsNull();
        await Assert.That(host.IsPrivateRepositoryAsync).Throws<BuildFailedException>();

        var publisher = host.CreateReleasePublisher();
        await publisher.BeginAsync().ConfigureAwait(false);
        await publisher.PublishAsync("1.0.0", "abc123", []).ConfigureAwait(false);
        await publisher.UndoAsync().ConfigureAwait(false);

        var notice = reporter.Messages.Single(x => x.Level == MessageLevel.Notice).Message;
        await Assert.That(notice).IsEqualTo("No release page created for 1.0.0: bv cannot publish a release on unknown host (git.example.invalid).");
    }

    [Test]
    [NotInParallel]
    public async Task GetToken_ReadsTheVariable_AtTheMomentOfTheCall()
    {
        const string variable = "BV_TEST_GIT_HOST_TOKEN";
        var host = new RecordingHost { TokenVariable = variable };
        try
        {
            Environment.SetEnvironmentVariable(variable, null);
            await Assert.That(host.GetToken()).IsNull();
            Environment.SetEnvironmentVariable(variable, "secret");
            await Assert.That(host.GetToken()).IsEqualTo("secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Test]
    public async Task GitRef_RejectsAnEmptyName()
    {
        await Assert.That(() => GitRef.Tag(string.Empty)).Throws<ArgumentException>();
        await Assert.That(() => GitRef.Branch(string.Empty)).Throws<ArgumentException>();
        await Assert.That(() => GitRef.Commit(string.Empty)).Throws<ArgumentException>();
        await Assert.That(GitRef.Commit("abc123").Kind).IsEqualTo(GitRefKind.Commit);
    }

    private static RepositoryConfig Repository(string? url) => new() { Url = url };

    private static GitUrlInfo Parse(string url) => GitUrlInfo.TryCreate(url, out var info) ? info : throw new InvalidOperationException(url);

    private static ServiceProvider Services(string url)
        => new ServiceCollection()
            .AddSingleton(new BuildvanaConfig { Repository = Repository(url) })
            .AddSingleton<CIPlatform>(new FakeCIPlatform())
            .AddSingleton<IReporter>(NullReporter.Instance)
            .BuildServiceProvider();
}
