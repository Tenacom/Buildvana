// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Buildvana.Sdk;

/// <summary>
/// An <see cref="ILogger"/> that forwards log entries to MSBuild's
/// <see cref="TaskLoggingHelper"/>, querying <see cref="IBuildEngine10.EngineServices"/>
/// when available to avoid formatting messages whose importance MSBuild would discard.
/// </summary>
internal sealed class TaskLoggingHelperLogger : ILogger
{
    private readonly TaskLoggingHelper _log;
    private readonly EngineServices? _engineServices;

    public TaskLoggingHelperLogger(TaskLoggingHelper log, IBuildEngine engine)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(engine);
        _log = log;
        _engineServices = (engine as IBuildEngine10)?.EngineServices;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullLogger.Instance.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => LogsMessagesOfImportance(MessageImportance.Low),
        LogLevel.Debug => LogsMessagesOfImportance(MessageImportance.Low),
        LogLevel.Information => LogsMessagesOfImportance(MessageImportance.Normal),
        LogLevel.Warning => true,
        LogLevel.Error => true,
        LogLevel.Critical => true,
        LogLevel.None => false,
        _ => false,
    };

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message = message + Environment.NewLine + exception;
        }

        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                _log.LogMessage(MessageImportance.Low, "{0}", message);
                break;
            case LogLevel.Information:
                _log.LogMessage(MessageImportance.Normal, "{0}", message);
                break;
            case LogLevel.Warning:
                _log.LogWarning("{0}", message);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _log.LogError("{0}", message);
                break;
        }
    }

    private bool LogsMessagesOfImportance(MessageImportance importance)
        => _engineServices?.LogsMessagesOfImportance(importance) ?? true;
}
