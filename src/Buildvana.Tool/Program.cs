// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.Cli;
using Buildvana.Tool.Configuration;
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
        // Force English help/diagnostics: Spectre.Console.Cli localizes via CurrentUICulture, and the invariant fallback is the English Resources.resx.
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        var console = AnsiConsole.Console;

        try
        {
            var (spectreArgs, forwardedArgs, globals) = PreprocessArgs(args);

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

            // Parse --verbosity eagerly (so the error surfaces in the outer catch) but defer SpectreLoggerProvider
            // construction to the DI factory below, so the container owns disposal.
            var initialLogLevel = globals.Verbosity is null ? LogLevel.Information : ParseVerbosity(globals.Verbosity);

            var services = new ServiceCollection()
                .AddSingleton(console)
                .AddSingleton<SpectreLoggerProvider>(_ => new SpectreLoggerProvider(console) { MinLevel = initialLogLevel })
                .AddSingleton<ILoggerProvider>(static sp => sp.GetRequiredService<SpectreLoggerProvider>())
                .AddSingleton(globals)
                .AddSingleton(new ForwardedArguments { Args = forwardedArgs })
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
                .AddSingleton(static _ => ToolConfiguration.FromEnvironment())
                .AddSingleton(static _ => NuGetPushConfiguration.FromEnvironment())
                .AddSingleton<SelfReferenceUpdater>();

            var registrar = new TypeRegistrar(services);
            var app = new CommandApp(registrar);
            app.Configure(config =>
            {
                config.Settings.CaseSensitivity = CaseSensitivity.None;
                config.SetApplicationName("bv");
                config.SetHelpProvider(new BvHelpProvider(config.Settings));
                config.AddCommand<CleanCommand>("clean");
                config.AddCommand<RestoreCommand>("restore");
                config.AddCommand<BuildCommand>("build");
                config.AddCommand<TestCommand>("test");
                config.AddCommand<PackCommand>("pack");
                config.AddCommand<ReleaseCommand>("release");
            });

            return await app.RunAsync(spectreArgs).ConfigureAwait(false);
        }
        catch (BuildFailedException ex)
        {
            console.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return ex.ExitCode;
        }
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

    private static (string[] SpectreArgs, IReadOnlyList<string> ForwardedArgs, GlobalOptions Globals) PreprocessArgs(string[] args)
    {
        var nonGlobal = new List<string>(args.Length);
        string? verbosity = null;
        string? mainBranch = null;
        var color = false;
        var noColor = false;
        var nologo = false;
        var version = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (TryNormalizeHelp(arg, out var canonical))
            {
                nonGlobal.Add(canonical);
                continue;
            }

            if (TryMatchBooleanGlobal(arg, ref color, ref noColor, ref nologo, ref version))
            {
                continue;
            }

            if (TryMatchValueGlobal(arg, args, ref i, shortName: "-v", longName: "--verbosity", ref verbosity))
            {
                continue;
            }

            if (TryMatchValueGlobal(arg, args, ref i, shortName: null, longName: "--main-branch", ref mainBranch))
            {
                continue;
            }

            nonGlobal.Add(arg);
        }

        var globals = new GlobalOptions(verbosity, color, noColor, nologo, version, mainBranch);
        var (spectreArgs, forwardedArgs) = SplitCommandArgs(nonGlobal);
        return (spectreArgs, forwardedArgs, globals);
    }

    // Splits the post-global tokens into the args handed to Spectre and the args forwarded verbatim to the
    // underlying `dotnet` invocation. The subcommand is the first token that is not an option (does not start
    // with '-'); globals can appear on either side of it, so forwarded args are not simply a trailing tail.
    private static (string[] SpectreArgs, IReadOnlyList<string> ForwardedArgs) SplitCommandArgs(List<string> nonGlobal)
    {
        var subcommandIndex = -1;
        for (var i = 0; i < nonGlobal.Count; i++)
        {
            if (!nonGlobal[i].StartsWith('-'))
            {
                subcommandIndex = i;
                break;
            }
        }

        // No subcommand (e.g. `bv`, `bv --help`): hand everything to Spectre (root help / error).
        if (subcommandIndex < 0)
        {
            return ([..nonGlobal], []);
        }

        // Commands with a fixed option surface (e.g. release) bind their arguments through Spectre.
        var subcommand = nonGlobal[subcommandIndex];
        if (!ConsumeAllArgumentsCommands.Names.Contains(subcommand))
        {
            return ([..nonGlobal], []);
        }

        // ConsumeAllArguments command: if help was requested, let Spectre render bv's help for it; otherwise
        // hand Spectre only the command name and stash the rest for verbatim forwarding.
        var helpRequested = nonGlobal.Contains("--help") || nonGlobal.Contains("-h");
        if (helpRequested)
        {
            return ([subcommand, "--help"], []);
        }

        var forwarded = new List<string>(nonGlobal.Count - 1);
        for (var i = 0; i < nonGlobal.Count; i++)
        {
            if (i != subcommandIndex)
            {
                forwarded.Add(nonGlobal[i]);
            }
        }

        return ([subcommand], forwarded);
    }

    // Spectre's built-in --help / -h matcher is hardcoded StringComparer.Ordinal (CaseSensitivity
    // setting doesn't cover it). Normalize case-variants to canonical lowercase so the case-insensitive
    // contract holds across all of bv's options, including the built-in help flag.
    private static bool TryNormalizeHelp(string arg, [NotNullWhen(true)] out string? canonical)
    {
        if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase))
        {
            canonical = "--help";
            return true;
        }

        if (string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
        {
            canonical = "-h";
            return true;
        }

        canonical = null;
        return false;
    }

    // Boolean global flags (case-insensitive, matching the rest of bv's option surface).
    private static bool TryMatchBooleanGlobal(string arg, ref bool color, ref bool noColor, ref bool nologo, ref bool version)
    {
        if (string.Equals(arg, "--nologo", StringComparison.OrdinalIgnoreCase))
        {
            nologo = true;
            return true;
        }

        if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase))
        {
            version = true;
            return true;
        }

        if (string.Equals(arg, "--color", StringComparison.OrdinalIgnoreCase))
        {
            color = true;
            return true;
        }

        if (string.Equals(arg, "--no-color", StringComparison.OrdinalIgnoreCase))
        {
            noColor = true;
            return true;
        }

        return false;
    }

    // Value-bearing global option (case-insensitive, matching the rest of bv's option surface). Accepts both
    // "<name> <value>" (value as the next token) and "<name>=<value>" inline forms. <paramref name="shortName"/>
    // may be null for options that have no short alias.
    private static bool TryMatchValueGlobal(string arg, string[] args, ref int i, string? shortName, string longName, ref string? value)
    {
        var isShort = shortName is not null && string.Equals(arg, shortName, StringComparison.OrdinalIgnoreCase);
        var isLong = string.Equals(arg, longName, StringComparison.OrdinalIgnoreCase);
        if (isShort || isLong)
        {
            if (i + 1 >= args.Length)
            {
                throw new BuildFailedException($"Option '{arg}' requires a value.");
            }

            value = args[++i];
            return true;
        }

        var longPrefix = longName + "=";
        if (arg.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[longPrefix.Length..];
            return true;
        }

        if (shortName is not null)
        {
            var shortPrefix = shortName + "=";
            if (arg.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg[shortPrefix.Length..];
                return true;
            }
        }

        return false;
    }
}
