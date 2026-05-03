// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Cake.Core.Diagnostics;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// An <see cref="ILoggerProvider"/> that forwards log entries to Cake's <see cref="ICakeLog"/>.
/// </summary>
internal sealed partial class CakeLogLoggerProvider : ILoggerProvider
{
    private readonly ICakeLog _log;

    public CakeLogLoggerProvider(ICakeLog log)
    {
        Guard.IsNotNull(log);
        _log = log;
    }

    public ILogger CreateLogger(string categoryName) => new CakeLogLogger(_log);

    public void Dispose()
    {
    }
}
