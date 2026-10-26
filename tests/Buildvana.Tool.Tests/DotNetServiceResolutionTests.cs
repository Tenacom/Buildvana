// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure.DependencyInjection;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Platforms;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

// A build pipeline command needs no Git repository: Buildvana SDK computes the version itself, and the one member
// of DotNetService that reads the version is the NuGet push. The home directory of these tests is not a Git
// repository, so VersionService cannot be constructed, and the build pipeline resolves all the same.
internal sealed class DotNetServiceResolutionTests
{
    private static readonly string[] PlatformVariables = ["FORGEJO_ACTIONS", "GITEA_ACTIONS", "GITHUB_ACTIONS", "GITLAB_CI"];

    [Test]
    public async Task GetRequiredService_OnLocalPlatform_ResolvesBuildPipelineWithoutGitRepository()
    {
        using var home = CreateHome();
        using var console = new TestConsole();
        var processRunner = new FakeProcessRunner();
        var services = BuildServiceProvider(home, console, processRunner, new LocalPlatform());
        await using (services.ConfigureAwait(false))
        {
            await Assert.That(services.GetRequiredService<VersionService>).Throws<BuildFailedException>();
            await Assert.That(services.GetRequiredService<BuildCommand>()).IsNotNull();

            await BuildAsync(services).ConfigureAwait(false);
            await Assert.That(processRunner.Runs[0].Args).Contains("-p:ContinuousIntegrationBuild=false");
        }
    }

    // The build pipeline depends on CIPlatform alone, which reads the environment and nothing else, so neither
    // the Git repository nor the Git host is ever opened.
    [Test]
    [NotInParallel]
    public async Task GetRequiredService_OnGitHubActions_ResolvesBuildPipelineWithoutGitRepository()
    {
        var originals = PlatformVariables.ToDictionary(x => x, Environment.GetEnvironmentVariable);
        foreach (var name in PlatformVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
        try
        {
            using var home = CreateHome();
            using var console = new TestConsole();
            var processRunner = new FakeProcessRunner();
            var services = BuildServiceProvider(home, console, processRunner, platform: null);
            await using (services.ConfigureAwait(false))
            {
                await Assert.That(services.GetRequiredService<CIPlatform>()).IsTypeOf<GitHubActionsPlatform>();
                await Assert.That(services.GetRequiredService<VersionService>).Throws<BuildFailedException>();
                await Assert.That(services.GetRequiredService<BuildCommand>()).IsNotNull();

                await BuildAsync(services).ConfigureAwait(false);
                await Assert.That(processRunner.Runs[0].Args).Contains("-p:ContinuousIntegrationBuild=true");
            }
        }
        finally
        {
            foreach (var (name, value) in originals)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static TempHome CreateHome()
    {
        var home = new TempHome();
        home.WriteFile("Test.slnx", "<Solution />\n");
        return home;
    }

    private static Task BuildAsync(ServiceProvider services)
        => services.GetRequiredService<DotNetService>().BuildSolutionAsync(
            services.GetRequiredService<SolutionContext>(),
            "Release",
            restore: false);

    // A null platform leaves the detection of AddBvServices in place.
    private static ServiceProvider BuildServiceProvider(
        TempHome home,
        IAnsiConsole console,
        FakeProcessRunner processRunner,
        CIPlatform? platform)
    {
        var collection = new ServiceCollection()
            .AddSingleton(console)
            .AddSingleton<IReporter>(new CaptureReporter())
            .AddSingleton(new GlobalSettings(null, false, false, true, true, true, false))
            .AddSingleton(new CommandParameters([], [], []))
            .AddSingleton<IHomeDirectoryProvider>(home.Provider)
            .AddBvServices()

            // The boundaries, faked. Registered last, so that they win over what AddBvServices registers.
            .AddSingleton<IProcessRunner>(processRunner);
        if (platform is not null)
        {
            _ = collection.AddSingleton(platform);
        }

        return collection.BuildServiceProvider();
    }
}
