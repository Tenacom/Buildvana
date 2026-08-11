// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Infrastructure.DependencyInjection;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services;
using Buildvana.Tool.Subcommands;
using Buildvana.Tool.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Buildvana.Tool;

[ExcludeFromCodeCoverage(Justification =
    "Process composition root (Ctrl-C handling, top-level exception mapping); exercised end to end, not unit-testable.")]
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

            // Delegation comes before everything else — the logo, --version, command resolution, argument
            // validation, configuration loading: when the repository's tool manifest pins bv, every judgment
            // about this invocation belongs to the pinned version, which prints its own logo and parses the
            // arguments itself.
            var delegatedExitCode = await TryDelegateAsync(args, parsed, globals).ConfigureAwait(false);
            if (delegatedExitCode is { } delegated)
            {
                return delegated;
            }

            if (globals.Version)
            {
                console.WriteLine(ThisAssembly.AssemblyInformationalVersion);
                return 0;
            }

            // The logo is narration, not a deliverable: it goes to standard error so that piped standard
            // output stays clean even without --nologo.
            if (!globals.Nologo)
            {
                await Console.Error.WriteLineAsync($"Buildvana CLI tool v{ThisAssembly.AssemblyInformationalVersion}").ConfigureAwait(false);
                await Console.Error.WriteLineAsync().ConfigureAwait(false);
            }

            var help = new BvHelpRenderer(console);
            if (parsed.Subcommand is null)
            {
                help.WriteRootHelp();
                return 0;
            }

            var (node, positionals) = CommandRegistry.Resolve(parsed.Subcommand, parsed.Positionals);

            if (parsed.HelpRequested)
            {
                help.WriteNodeHelp(node);
                return 0;
            }

            // A pure command group invoked bare has nothing to execute: print its help, like bare `bv` prints the root help.
            var command = node.Command;
            if (command is null)
            {
                help.WriteNodeHelp(node);
                return 0;
            }

            CommandArgumentValidator.Validate(command, parsed, positionals);

            // Parse --verbosity eagerly so an invalid value surfaces in the outer catch.
            // When absent, the command's own default applies (query commands default to Minimal).
            var verbosity = globals.Verbosity is null ? command.DefaultVerbosity : VerbosityParser.Parse(globals.Verbosity);

            // --color / --no-color win over auto-detection; neither (or both) leaves the reporter to auto-detect.
            bool? colorOverride = (globals.Color, globals.NoColor) switch
            {
                (true, false) => true,
                (false, true) => false,
                _ => null,
            };
            reporter = new ConsoleReporter(verbosity, colorOverride);

            var services = BuildServiceProvider(console, reporter, globals, parsed, positionals);
            await using (services.ConfigureAwait(false))
            {
                if (command.UsesSdk && !globals.SkipSdkCheck)
                {
                    services.GetRequiredService<SelfVersionService>().EnsureSdkVersionMatch();
                }

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

    private static Task<int?> TryDelegateAsync(string[] args, ParsedCommandLine parsed, GlobalSettings globals)
    {
        var ownVersion = OwnVersion.Value;
        var layout = InstallLayoutDetector.Detect(
            AppContext.BaseDirectory,
            ToolManifest.BvPackageId,
            ownVersion.ToNormalizedString());
        var delegation = new DelegationService(
            new JsonHelper(),
            new ProcessRunner(),
            ToolResolverCacheProbe.CreateDefault(),
            ownVersion,
            Console.Error);
        var context = new DelegationContext(
            args,
            parsed.Subcommand,
            globals.SkipDelegation,
            Environment.GetEnvironmentVariable(DelegationService.DelegatedEnvVar) is not null,
            layout,
            Environment.CurrentDirectory);
        return delegation.TryDelegateAsync(context);
    }

    private static ServiceProvider BuildServiceProvider(
        IAnsiConsole console,
        IReporter reporter,
        GlobalSettings globals,
        ParsedCommandLine parsed,
        IReadOnlyList<string> positionals)
    {
        return new ServiceCollection()
            .AddSingleton(console)
            .AddSingleton(reporter)
            .AddSingleton(globals)
            .AddSingleton(new CommandParameters(parsed.OptionTokens, positionals, parsed.Forwarded))
            .AddSingleton<IHomeDirectoryProvider>(static _ => new AnchoringHomeDirectoryProvider(
                new DiscoveredHomeDirectoryProvider(Environment.CurrentDirectory)))
            .AddBvServices()
            .BuildServiceProvider();
    }
}
