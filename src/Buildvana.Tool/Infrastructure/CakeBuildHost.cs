// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Buildvana.Core;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.Diagnostics;
using CommunityToolkit.Diagnostics;
using LogLevel = Buildvana.Core.LogLevel;

namespace Buildvana.Tool.Infrastructure;

public sealed class CakeBuildHost : IBuildHost
{
    private readonly ICakeContext _context;

    public CakeBuildHost(ICakeContext context)
    {
        Guard.IsNotNull(context);
        _context = context;
    }

    [DoesNotReturn]
    public void Fail(string message)
    {
        _context.Error(message);
        throw new CakeException(message);
    }

    public bool IsEnabled(LogLevel level) => level switch
    {
        LogLevel.Trace => _context.Log.Verbosity >= Verbosity.Diagnostic,
        LogLevel.Debug => _context.Log.Verbosity >= Verbosity.Verbose,
        LogLevel.Information => _context.Log.Verbosity >= Verbosity.Normal,
        LogLevel.Warning => _context.Log.Verbosity >= Verbosity.Minimal,
        LogLevel.Error => _context.Log.Verbosity >= Verbosity.Quiet,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    public void Log(LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Trace:
                _context.Debug(message);
                break;
            case LogLevel.Debug:
                _context.Verbose(message);
                break;
            case LogLevel.Information:
                _context.Information(message);
                break;
            case LogLevel.Warning:
                _context.Warning(message);
                break;
            case LogLevel.Error:
                _context.Error(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }
}
