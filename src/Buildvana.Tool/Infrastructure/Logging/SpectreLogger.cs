// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Buildvana.Tool.Infrastructure.Logging;

internal sealed class SpectreLogger : ILogger
{
    private readonly IAnsiConsole _console;
    private readonly string _categoryName;

    public SpectreLogger(IAnsiConsole console, string categoryName)
    {
        _console = console;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Guard.IsNotNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var (style, label) = logLevel switch {
            LogLevel.Trace => ("grey", "trce"),
            LogLevel.Debug => ("grey", "dbug"),
            LogLevel.Information => ("white", "info"),
            LogLevel.Warning => ("yellow", "warn"),
            LogLevel.Error => ("red", "fail"),
            LogLevel.Critical => ("red bold", "crit"),
            _ => ("white", "????"),
        };

        // Markup.Escape: log messages may contain '[' or ']' which Spectre would otherwise interpret as markup.
        _console.MarkupLine(
            $"[{style}]{label}[/]: [silver]{Markup.Escape(_categoryName)}[/]: {Markup.Escape(message)}");

        if (exception is not null)
        {
            _console.WriteException(exception);
        }
    }
}
