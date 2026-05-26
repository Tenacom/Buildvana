// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Configuration;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Infrastructure.Logging;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Buildvana.Tool;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var console = AnsiConsole.Console;

        try
        {
            var parsed = CliArgSplitter.Split(args);
            var globals = parsed.Globals;

            // Apply --color / --no-color before any output. When both (or neither) are set the existing console profile wins.
            if (globals.Color != globals.NoColor)
            {
                console.Profile.Capabilities.Ansi = globals.Color;
            }

            if (globals.Version)
            {
                console.WriteLine(ThisAssembly.AssemblyInformationalVersion);
                return 0;
            }

            if (!globals.Nologo)
            {
                console.WriteLine($"Buildvana CLI tool v{ThisAssembly.AssemblyInformationalVersion}");
                console.WriteLine();
            }

            var help = new BvHelpRenderer(console);
            if (parsed.Subcommand is null)
            {
                help.WriteRootHelp();
                return 0;
            }

            var command = CommandRegistry.Find(parsed.Subcommand)
                ?? throw new BuildFailedException($"Unknown command '{parsed.Subcommand}'. Run 'bv --help' for the list of commands.");

            if (parsed.HelpRequested)
            {
                help.WriteCommandHelp(command);
                return 0;
            }

            CommandArgumentValidator.Validate(command, parsed);

            // Parse --verbosity eagerly (so the error surfaces in the outer catch) but defer SpectreLoggerProvider
            // construction to the DI factory below, so the container owns disposal.
            var initialLogLevel = globals.Verbosity is null ? LogLevel.Information : ParseVerbosity(globals.Verbosity);

            var services = BuildServiceProvider(console, globals, parsed, initialLogLevel);
            await using (services.ConfigureAwait(false))
            {
                using var cts = new CancellationTokenSource();
                void OnCancel(object? sender, ConsoleCancelEventArgs e)
                {
                    // Suppress the immediate process kill so commands can observe the token; child processes
                    // still receive their own Ctrl-C from the console and terminate on their own.
                    e.Cancel = true;

                    // Safe: the handler is removed in the finally below before cts is disposed at end of scope.
                    // ReSharper disable once AccessToDisposedClosure
                    cts.Cancel();
                }

                Console.CancelKeyPress += OnCancel;
                try
                {
                    var instance = (IBvCommand)services.GetRequiredService(command.CommandType);
                    return await instance.ExecuteAsync(cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    Console.CancelKeyPress -= OnCancel;
                }
            }
        }
        catch (BuildFailedException ex)
        {
            console.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return ex.ExitCode;
        }
    }

    private static ServiceProvider BuildServiceProvider(
        IAnsiConsole console,
        GlobalSettings globals,
        ParsedCommandLine parsed,
        LogLevel initialLogLevel)
    {
        var services = new ServiceCollection()
            .AddSingleton(console)
            .AddSingleton<SpectreLoggerProvider>(_ => new SpectreLoggerProvider(console) { MinLevel = initialLogLevel })
            .AddSingleton<ILoggerProvider>(static sp => sp.GetRequiredService<SpectreLoggerProvider>())
            .AddSingleton(globals)
            .AddSingleton(new CommandParameters { Options = parsed.OptionTokens, Forwarded = parsed.Forwarded })
            .AddSingleton(static sp => ReleaseSettings.Parse(sp.GetRequiredService<CommandParameters>().Options))
            .AddLogging(static builder => builder.SetMinimumLevel(LogLevel.Trace))
            .AddSingleton<IHomeDirectoryProvider>(static _ => new DiscoveredHomeDirectoryProvider(Environment.CurrentDirectory))

            // Lazy by design: this factory (and thus discovery, parsing, and validation) runs on first resolve.
            // No Phase 1 command resolves BuildvanaConfig, so a malformed buildvana.json stays inert until a
            // Phase 2 consumer reads it.
            .AddSingleton(static sp => BuildvanaConfigLoader.Load(sp.GetRequiredService<IHomeDirectoryProvider>().HomeDirectory))
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
            .AddSingleton(static _ => ToolConfiguration.FromEnvironment())
            .AddSingleton(static _ => NuGetPushConfiguration.FromEnvironment())
            .AddSingleton<SelfReferenceUpdater>();

        foreach (var registration in CommandRegistry.Commands)
        {
            services.AddSingleton(registration.CommandType);
        }

        return services.BuildServiceProvider();
    }

    private static LogLevel ParseVerbosity(string raw) => raw.ToUpperInvariant() switch
    {
        "QUIET" or "Q" => LogLevel.Error,
        "MINIMAL" or "M" => LogLevel.Warning,
        "NORMAL" or "N" => LogLevel.Information,
        "DETAILED" or "D" => LogLevel.Debug,
        "DIAGNOSTIC" or "DIAG" => LogLevel.Trace,
        _ => throw new BuildFailedException($"Unknown verbosity level '{raw}'. Use one of: quiet, minimal, normal, detailed, diagnostic."),
    };
}
