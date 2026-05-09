// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;
using Buildvana.Tool.Infrastructure.Logging;
using Buildvana.Tool.Services;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Applies parsed Spectre command settings to runtime services: logger min level, console color profile,
/// and named-option pushes into <see cref="OptionsService"/>.
/// </summary>
internal static class SettingsApplier
{
    public static void Apply(CommandSettings settings, IServiceProvider services)
    {
        Guard.IsNotNull(settings);
        Guard.IsNotNull(services);

        if (settings is BaseSettings @base)
        {
            ApplyVerbosity(@base.Verbosity, services);
            ApplyColor(@base.Color, @base.NoColor, services);
        }

        var options = services.GetRequiredService<OptionsService>();
        if (settings is BuildSettings build)
        {
            SetIfPresent(options, "configuration", build.Configuration);
            SetIfPresent(options, "mainBranch", build.MainBranch);
        }

        if (settings is ReleaseSettings release)
        {
            SetIfPresent(options, "bump", release.Bump);
            SetIfPresent(options, "checkPublicApi", release.CheckPublicApi);
            SetIfPresent(options, "unstableChangelog", release.UnstableChangelog);
            SetIfPresent(options, "requireChangelog", release.RequireChangelog);
            SetIfPresent(options, "dogfood", release.Dogfood);
        }
    }

    private static void SetIfPresent(OptionsService options, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            options.SetOption(name, value);
        }
    }

    private static void SetIfPresent(OptionsService options, string name, bool? value)
    {
        if (value.HasValue)
        {
            options.SetOption(name, value.Value ? "true" : "false");
        }
    }

    private static void ApplyVerbosity(string? raw, IServiceProvider services)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        var level = ParseVerbosity(raw);
        services.GetRequiredService<SpectreLoggerProvider>().MinLevel = level;
    }

    private static LogLevel ParseVerbosity(string raw) => raw.ToUpperInvariant() switch
    {
        "QUIET" => LogLevel.Critical,
        "MINIMAL" => LogLevel.Warning,
        "NORMAL" => LogLevel.Information,
        "VERBOSE" or "DEBUG" => LogLevel.Debug,
        "DIAGNOSTIC" or "TRACE" => LogLevel.Trace,
        "INFO" or "INFORMATION" => LogLevel.Information,
        "WARN" or "WARNING" => LogLevel.Warning,
        "ERROR" => LogLevel.Error,
        "CRITICAL" => LogLevel.Critical,
        "NONE" => LogLevel.None,
        _ => throw new BuildFailedException($"Unknown verbosity level '{raw}'. Use one of: Quiet, Minimal, Normal, Verbose, Diagnostic, Trace, Debug, Information, Warning, Error, Critical, None."),
    };

    private static void ApplyColor(bool color, bool noColor, IServiceProvider services)
    {
        if (color == noColor)
        {
            return;
        }

        var console = services.GetRequiredService<IAnsiConsole>();
        console.Profile.Capabilities.Ansi = color;
    }
}
