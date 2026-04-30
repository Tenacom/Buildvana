// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Buildvana.Core;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Buildvana.Sdk;

internal sealed class MSBuildTaskHost : IBuildHost
{
    private readonly TaskLoggingHelper _log;
    private readonly EngineServices? _engineServices;

    public MSBuildTaskHost(TaskLoggingHelper log, IBuildEngine engine)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(engine);
        _log = log;
        _engineServices = (engine as IBuildEngine10)?.EngineServices;
    }

    [DoesNotReturn]
    public void Fail(string message) => throw new BuildErrorException(message);

    public bool IsEnabled(LogLevel level) => level switch
    {
        LogLevel.Trace => LogsMessagesOfImportance(MessageImportance.Low),
        LogLevel.Debug => LogsMessagesOfImportance(MessageImportance.Low),
        LogLevel.Information => LogsMessagesOfImportance(MessageImportance.Normal),
        LogLevel.Warning => true,
        LogLevel.Error => true,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    public void Log(LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Trace:
                _log.LogMessage(MessageImportance.Low, message);
                break;
            case LogLevel.Debug:
                _log.LogMessage(MessageImportance.Low, message);
                break;
            case LogLevel.Information:
                _log.LogMessage(MessageImportance.Normal, message);
                break;
            case LogLevel.Warning:
                _log.LogWarning(message);
                break;
            case LogLevel.Error:
                _log.LogError(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

    private bool LogsMessagesOfImportance(MessageImportance importance)
        => _engineServices?.LogsMessagesOfImportance(importance) ?? true;
}
