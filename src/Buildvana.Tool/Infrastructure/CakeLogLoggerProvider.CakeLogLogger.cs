// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Cake.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CakeLogLevel = Cake.Core.Diagnostics.LogLevel;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Buildvana.Tool.Infrastructure;

partial class CakeLogLoggerProvider
{
    private sealed class CakeLogLogger : ILogger
    {
        private readonly ICakeLog _log;

        public CakeLogLogger(ICakeLog log)
        {
            _log = log;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None)
            {
                return false;
            }

            var (verbosity, _) = Map(logLevel);
            return _log.Verbosity >= verbosity;
        }

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

            var (verbosity, cakeLevel) = Map(logLevel);
            var message = formatter(state, exception);
            if (exception is not null)
            {
                message = message + Environment.NewLine + exception;
            }

            _log.Write(verbosity, cakeLevel, "{0}", message);
        }

        private static (Verbosity Verbosity, CakeLogLevel Level) Map(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => (Verbosity.Diagnostic, CakeLogLevel.Debug),
            LogLevel.Debug => (Verbosity.Verbose, CakeLogLevel.Verbose),
            LogLevel.Information => (Verbosity.Normal, CakeLogLevel.Information),
            LogLevel.Warning => (Verbosity.Minimal, CakeLogLevel.Warning),
            LogLevel.Error => (Verbosity.Quiet, CakeLogLevel.Error),
            LogLevel.Critical => (Verbosity.Quiet, CakeLogLevel.Fatal),
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null),
        };
    }
}
