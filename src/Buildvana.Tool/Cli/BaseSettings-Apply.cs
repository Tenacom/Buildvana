// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core;
using Buildvana.Tool.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Buildvana.Tool.Cli;

partial class BaseSettings
{
    /// <summary>
    /// Applies the global options carried by this settings object to the runtime services:
    /// logger min level (from <see cref="Verbosity"/>) and console color profile (from <see cref="Color"/>/<see cref="NoColor"/>).
    /// </summary>
    /// <param name="services">The service provider.</param>
    public void Apply(IServiceProvider services)
    {
        ApplyVerbosity(services);
        ApplyColor(services);
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

    private void ApplyVerbosity(IServiceProvider services)
    {
        if (string.IsNullOrEmpty(Verbosity))
        {
            return;
        }

        services.GetRequiredService<SpectreLoggerProvider>().MinLevel = ParseVerbosity(Verbosity);
    }

    private void ApplyColor(IServiceProvider services)
    {
        if (Color == NoColor)
        {
            return;
        }

        services.GetRequiredService<IAnsiConsole>().Profile.Capabilities.Ansi = Color;
    }
}
