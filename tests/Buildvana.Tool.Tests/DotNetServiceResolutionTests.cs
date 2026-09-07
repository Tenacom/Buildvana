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
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

internal sealed class DotNetServiceResolutionTests
{
    // A build pipeline command needs no Git repository: Buildvana SDK computes the version itself, and the one
    // member of DotNetService that reads the version is the NuGet push. The home directory below is not a Git
    // repository, so VersionService cannot be constructed, and DotNetService resolves all the same.
    [Test]
    public async Task GetRequiredService_HomeDirectoryIsNotGitRepository_ResolvesDotNetService()
    {
        using var home = new TempHome();
        using var console = new TestConsole();
        var services = BuildServiceProvider(home, console);
        await using (services.ConfigureAwait(false))
        {
            await Assert.That(services.GetRequiredService<VersionService>).Throws<BuildFailedException>();
            await Assert.That(services.GetRequiredService<DotNetService>()).IsNotNull();
        }
    }

    private static ServiceProvider BuildServiceProvider(TempHome home, IAnsiConsole console)
        => new ServiceCollection()
            .AddSingleton(console)
            .AddSingleton<IReporter>(new CaptureReporter())
            .AddSingleton(new GlobalSettings(null, false, false, true, true, true, false))
            .AddSingleton(new CommandParameters([], [], []))
            .AddSingleton<IHomeDirectoryProvider>(home.Provider)
            .AddBvServices()

            // The boundaries, faked. Registered last, so that they win over what AddBvServices registers.
            .AddSingleton<IProcessRunner>(new FakeProcessRunner())
            .AddSingleton<ServerAdapter>(static sp => new RecordingServerAdapter(sp) { CloudBuild = false })
            .BuildServiceProvider();
}
