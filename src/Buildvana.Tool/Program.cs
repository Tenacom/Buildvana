// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
            var (cleanArgs, msbuildProperties, globals) = PreprocessArgs(args);

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
                .AddSingleton<BuildSettingsHolder>()
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

            return await app.RunAsync(cleanArgs).ConfigureAwait(false);
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

    private static (string[] CleanArgs, MSBuildProperties Properties, GlobalOptions Globals) PreprocessArgs(string[] args)
    {
        var cleanArgs = new List<string>(args.Length);
        var properties = new MSBuildProperties();
        string? verbosity = null;
        var color = false;
        var noColor = false;
        var nologo = false;
        var version = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // Spectre's built-in --help / -h matcher is hardcoded StringComparer.Ordinal (CaseSensitivity
            // setting doesn't cover it). Normalize case-variants to canonical lowercase so the case-insensitive
            // contract holds across all of bv's options, including the built-in help flag.
            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase))
            {
                cleanArgs.Add("--help");
                continue;
            }

            if (string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
            {
                cleanArgs.Add("-h");
                continue;
            }

            // Boolean global flags (case-insensitive, matching the rest of bv's option surface).
            if (string.Equals(arg, "--nologo", StringComparison.OrdinalIgnoreCase))
            {
                nologo = true;
                continue;
            }

            if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase))
            {
                version = true;
                continue;
            }

            if (string.Equals(arg, "--color", StringComparison.OrdinalIgnoreCase))
            {
                color = true;
                continue;
            }

            if (string.Equals(arg, "--no-color", StringComparison.OrdinalIgnoreCase))
            {
                noColor = true;
                continue;
            }

            // --verbosity / -v with value as the next token.
            if (string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--verbosity", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    throw new BuildFailedException($"Option '{arg}' requires a value (verbosity level).");
                }

                verbosity = args[++i];
                continue;
            }

            // --verbosity=VALUE / -v=VALUE inline form.
            if (arg.StartsWith("--verbosity=", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = arg["--verbosity=".Length..];
                continue;
            }

            if (arg.StartsWith("-v=", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = arg[3..];
                continue;
            }

            // MSBuild properties (forwarded to the underlying invocation, not bv's own options).
            if (arg.Length > 3
                && (arg.StartsWith("/p:", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-p:", StringComparison.OrdinalIgnoreCase)))
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

        return ([..cleanArgs], properties, new GlobalOptions(verbosity, color, noColor, nologo, version));
    }
}
