// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.Build;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Configuration;
using Buildvana.Tool.Infrastructure.DependencyInjection;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Buildvana.Tool;

internal static class Program
{
    // 128 + SIGINT (2): the POSIX convention for a process terminated by Ctrl-C.
    private const int CancelledExitCode = 130;

    public static async Task<int> Main(string[] args)
    {
        var console = AnsiConsole.Console;

        // Assigned once --verbosity and --color/--no-color are known. The outer catch falls back to a default
        // reporter for errors that occur before that point (e.g. an invalid --verbosity value).
        IReporter? reporter = null;

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

            // Parse --verbosity eagerly so an invalid value surfaces in the outer catch.
            var verbosity = globals.Verbosity is null ? Verbosity.Normal : ParseVerbosity(globals.Verbosity);

            // --color / --no-color win over auto-detection; neither (or both) leaves the reporter to auto-detect.
            bool? colorOverride = (globals.Color, globals.NoColor) switch
            {
                (true, false) => true,
                (false, true) => false,
                _ => null,
            };
            reporter = new ConsoleReporter(verbosity, colorOverride);

            var services = BuildServiceProvider(reporter, globals, parsed);
            await using (services.ConfigureAwait(false))
            {
                var cts = new CancellationTokenSource();

                // Serializes the Ctrl-C handler with cts disposal in the finally block below. Unsubscribing the
                // handler does not wait for an in-flight invocation, so without this gate a cts.Cancel() racing
                // cts.Dispose() could throw ObjectDisposedException on the handler thread.
                var cancelGate = new Lock();
                var ctsDisposed = false;
                void OnCancel(object? sender, ConsoleCancelEventArgs e)
                {
                    // Suppress bv's own immediate termination so the command can observe the token and shut down
                    // cleanly: the token is forwarded down to the running `dotnet` child, whose process tree is
                    // then killed.
                    e.Cancel = true;

                    lock (cancelGate)
                    {
                        // ReSharper disable once AccessToModifiedClosure
                        if (ctsDisposed)
                        {
                            return;
                        }

                        // ReSharper disable once AccessToDisposedClosure
                        cts.Cancel();
                    }
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
                    lock (cancelGate)
                    {
                        ctsDisposed = true;
                        cts.Dispose();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            (reporter ?? CreateDefaultReporter()).Error("Operation cancelled.");
            return CancelledExitCode;
        }
        catch (BuildFailedException ex)
        {
            var activeReporter = reporter ?? CreateDefaultReporter();
            activeReporter.Error(ex.Message);

            // Emit each diagnostic verbatim (no level label or color) in canonical compiler format, so a
            // terminal such as VS Code renders the file(line,column) prefix as a clickable link.
            // Verbosity.Quiet guarantees we emit them at any verbosity level.
            foreach (var diagnostic in ex.Diagnostics)
            {
                activeReporter.ChildError(diagnostic.ToString(), Verbosity.Quiet);
            }

            return ex.ExitCode;
        }

        static IReporter CreateDefaultReporter() => new ConsoleReporter(Verbosity.Normal, colorOverride: null);
    }

    private static ServiceProvider BuildServiceProvider(
        IReporter reporter,
        GlobalSettings globals,
        ParsedCommandLine parsed)
    {
        var services = new ServiceCollection()
            .AddLazySupport()
            .AddSingleton(reporter)
            .AddSingleton(globals)
            .AddSingleton(new CommandParameters(parsed.OptionTokens, parsed.Forwarded))
            .AddSingleton(static sp => ReleaseSettings.Parse(sp.GetRequiredService<CommandParameters>().Options))
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
            .AddSingleton<BuildPipeline>()
            .AddSingleton(static _ => ToolConfiguration.FromEnvironment())
            .AddSingleton(static _ => NuGetPushConfiguration.FromEnvironment())
            .AddSingleton<SelfReferenceUpdater>();

        foreach (var registration in CommandRegistry.Commands)
        {
            services.AddSingleton(registration.CommandType);
        }

        return services.BuildServiceProvider();
    }

    private static Verbosity ParseVerbosity(string raw) => raw.ToUpperInvariant() switch
    {
        "QUIET" or "Q" => Verbosity.Quiet,
        "MINIMAL" or "M" => Verbosity.Minimal,
        "NORMAL" or "N" => Verbosity.Normal,
        "DETAILED" or "D" => Verbosity.Detailed,
        "DIAGNOSTIC" or "DIAG" => Verbosity.Diagnostic,
        _ => throw new BuildFailedException($"Unknown verbosity level '{raw}'. Use one of: [q]uiet, [m]inimal, [n]ormal, [d]etailed, [diag]nostic."),
    };
}
