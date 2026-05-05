// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.Cli;
using Buildvana.Tool.Infrastructure.Logging;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildvana.Tool;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var (cleanArgs, msbuildProperties) = ExtractMSBuildProperties(args);

        var console = AnsiConsole.Console;

        var services = new ServiceCollection()
            .AddSingleton(console)
            .AddSingleton<SpectreLoggerProvider>()
            .AddSingleton<ILoggerProvider>(static sp => sp.GetRequiredService<SpectreLoggerProvider>())
            .AddSingleton(msbuildProperties)
            .AddLogging(static builder => builder.SetMinimumLevel(LogLevel.Trace))
            .AddSingleton<IHomeDirectoryProvider>(static _ => new DiscoveredHomeDirectoryProvider(Environment.CurrentDirectory))
            .AddSingleton<IJsonHelper, JsonHelper>()
            .AddSingleton<IProcessRunner, ProcessRunner>()
            .AddSingleton<ISolutionContextFactory, HomeDirectorySolutionContextFactory>()
            .AddSingleton<SolutionContext>(static sp => sp.GetRequiredService<ISolutionContextFactory>().Create())
            .AddSingleton<GitService>()
            .AddSingleton<PublicApiFilesService>()
            .AddSingleton(ServerAdapter.Create)
            .AddSingleton<VersionService>()
            .AddSingleton<ChangelogService>()
            .AddSingleton<DocFxService>()
            .AddSingleton<DotNetService>()
            .AddSingleton<OptionsService>()
            .AddSingleton<SelfReferenceUpdater>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("bv");
            config.AddCommand<CleanCommand>("clean");
            config.AddCommand<RestoreCommand>("restore");
            config.AddCommand<BuildCommand>("build");
            config.AddCommand<TestCommand>("test");
            config.AddCommand<PackCommand>("pack");
            config.AddCommand<ReleaseCommand>("release");
        });

        try
        {
            return await app.RunAsync(cleanArgs).ConfigureAwait(false);
        }
        catch (BuildFailedException ex)
        {
            console.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return ex.ExitCode;
        }
    }

    private static (string[] CleanArgs, MSBuildProperties Properties) ExtractMSBuildProperties(string[] args)
    {
        var cleanArgs = new List<string>(args.Length);
        var properties = new MSBuildProperties();
        foreach (var arg in args)
        {
            if (arg.Length > 3
                && (arg.StartsWith("/p:", StringComparison.Ordinal) || arg.StartsWith("-p:", StringComparison.Ordinal)))
            {
                var kv = arg[3..];
                var eq = kv.IndexOf('=', StringComparison.Ordinal);
                if (eq > 0)
                {
                    properties.Set(kv[..eq], kv[(eq + 1)..]);
                    continue;
                }
            }

            cleanArgs.Add(arg);
        }

        return ([..cleanArgs], properties);
    }
}
