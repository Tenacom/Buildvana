// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Testing;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure.DependencyInjection;
using Buildvana.Tool.Services.Platforms;
using Buildvana.Tool.Subcommands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

// `bv info` over the real service graph, with the CI platform faked.
internal sealed class InfoCommandTests
{
    private const string RepositoryUrl = "https://github.com/Tenacom/Buildvana";
    private const string TokenEnvVar = "BV_TEST_INFO_TOKEN";

    [Test]
    [NotInParallel]
    public async Task Info_OnAKnownHost_ReportsEveryLine()
    {
        using var repo = CreateRepo(("origin", "https://github.com/someone/Buildvana"), ("upstream", RepositoryUrl));
        repo.WriteFile("buildvana.json", $$"""
            {
              "repository": { "url": "{{RepositoryUrl}}" },
              "hosts": { "github": { "tokenEnv": "{{TokenEnvVar}}" } },
              "git": { "identity": { "name": "Release Bot", "email": "release@buildvana.invalid" } }
            }
            """);
        var platform = new FakeCIPlatform { Server = new("https://github.com") };
        var originalToken = Environment.GetEnvironmentVariable(TokenEnvVar);
        Environment.SetEnvironmentVariable(TokenEnvVar, "secret-token-value");
        try
        {
            var output = await RunAsync(repo.RootPath, platform).ConfigureAwait(false);

            await Assert.That(output).IsEqualTo("""
                CI platform:                Test CI platform
                Server URL:                 https://github.com/
                Bot identity:               Buildvana Test Bot <bot@buildvana.invalid>
                ContinuousIntegrationBuild: true
                Repository URL:             https://github.com/Tenacom/Buildvana
                Release remote:             upstream
                Git host:                   GitHub (github.com)
                Token variable:             BV_TEST_INFO_TOKEN (set)
                Release identity:           Release Bot <release@buildvana.invalid>, from git.identity
                """);
            await Assert.That(output).DoesNotContain("secret-token-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(TokenEnvVar, originalToken);
        }
    }

    [Test]
    public async Task Info_WithoutRepositoryUrl_ReportsTheReason()
    {
        using var repo = CreateRepo(("origin", RepositoryUrl));
        repo.WriteFile("buildvana.json", "{}");

        var report = ParseReport(await RunAsync(repo.RootPath, new FakeCIPlatform()).ConfigureAwait(false));

        await Assert.That(report["Repository URL"]).IsEqualTo("(none)");
        await Assert.That(report["Release remote"]).StartsWith("(none) The configuration file states no repository.url");
        await Assert.That(report["Git host"]).StartsWith("(none) The configuration file states no repository.url");
        await Assert.That(report["Token variable"]).IsEqualTo("(none)");
    }

    [Test]
    [Arguments(true, "unknown host (git.example.com), allowed by release.allowUnknownHost")]
    [Arguments(false, "unknown host (git.example.com), refused by bv release unless release.allowUnknownHost is true")]
    public async Task Info_OnAnUnknownHost_SaysWhetherAReleaseProceeds(bool allowUnknownHost, string expected)
    {
        const string unknownUrl = "https://git.example.com/Tenacom/Buildvana";
        using var repo = CreateRepo(("origin", unknownUrl));
        repo.WriteFile("buildvana.json", $$"""
            {
              "repository": { "url": "{{unknownUrl}}" },
              "release": { "allowUnknownHost": {{(allowUnknownHost ? "true" : "false")}} }
            }
            """);

        var report = ParseReport(await RunAsync(repo.RootPath, new FakeCIPlatform()).ConfigureAwait(false));

        await Assert.That(report["Release remote"]).IsEqualTo("origin");
        await Assert.That(report["Git host"]).IsEqualTo(expected);
        await Assert.That(report["Token variable"]).IsEqualTo("(none)");
    }

    [Test]
    public async Task Info_WithNeitherConfiguredNorBotIdentity_ReportsTheIdentityOfTheGitConfiguration()
    {
        using var repo = CreateRepo(("origin", RepositoryUrl));
        repo.SetCommitterIdentity("Local Dev", "dev@buildvana.invalid");
        repo.WriteFile("buildvana.json", $$"""
            {
              "repository": { "url": "{{RepositoryUrl}}" },
              "hosts": { "github": { "tokenEnv": "BV_TEST_INFO_UNSET_TOKEN" } }
            }
            """);
        var platform = new FakeCIPlatform { RunsOnCI = false, Identity = null };

        var report = ParseReport(await RunAsync(repo.RootPath, platform).ConfigureAwait(false));

        await Assert.That(report["Bot identity"]).IsEqualTo("(none)");
        await Assert.That(report["ContinuousIntegrationBuild"]).IsEqualTo("false");
        await Assert.That(report["Token variable"]).IsEqualTo("BV_TEST_INFO_UNSET_TOKEN (not set)");
        await Assert.That(report["Release identity"]).IsEqualTo("Local Dev <dev@buildvana.invalid>, from the Git configuration");
    }

    [Test]
    public async Task Info_OutsideAGitRepository_ReportsTheReason()
    {
        using var home = new TempHome();
        home.WriteFile("buildvana.json", $$"""{ "repository": { "url": "{{RepositoryUrl}}" } }""");

        var platform = new FakeCIPlatform { Identity = null };

        var report = ParseReport(await RunAsync(home.RootPath, platform).ConfigureAwait(false));

        await Assert.That(report["Release remote"]).StartsWith("(none) There is no Git repository at");
        await Assert.That(report["Git host"]).IsEqualTo("GitHub (github.com)");
        await Assert.That(report["Release identity"]).IsEqualTo("(none)");
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

    private static async Task<string> RunAsync(string homePath, CIPlatform platform)
    {
        using var console = new TestConsole();
        using var services = new ServiceCollection()
            .AddSingleton<IAnsiConsole>(console)
            .AddSingleton<IReporter>(NullReporter.Instance)
            .AddSingleton(new GlobalSettings(null, false, false, true, true, true, false))
            .AddSingleton(new CommandParameters([], [], []))
            .AddSingleton<IHomeDirectoryProvider>(new FixedHomeDirectoryProvider(homePath))
            .AddBvServices()
            .AddSingleton(platform)
            .BuildServiceProvider();

        var exitCode = await services.GetRequiredService<InfoCommand>().ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        await Assert.That(exitCode).IsEqualTo(0);
        return console.Output.ReplaceLineEndings("\n").TrimEnd('\n');
    }

    // The report, keyed by the label of each line, without the colon.
    private static Dictionary<string, string> ParseReport(string output)
        => output
            .Split('\n')
            .Select(static line => line.Split(':', 2))
            .ToDictionary(static parts => parts[0], static parts => parts[1].Trim());
}
