// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Buildvana.Tool.Infrastructure.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes log entries through an <see cref="IAnsiConsole"/>.
/// </summary>
/// <remarks>
/// <para>TTY awareness is delegated to the supplied <see cref="IAnsiConsole"/>: when output is redirected,
/// Spectre.Console's default profile downgrades to plain ASCII automatically, so this provider needs no
/// extra detection logic of its own.</para>
/// <para>The provider does not own the supplied console; <see cref="Dispose"/> is a no-op.</para>
/// </remarks>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - Will be instantiated via DI in a soon-to-come commit.
internal sealed class SpectreLoggerProvider : ILoggerProvider
#pragma warning restore
{
    private readonly IAnsiConsole _console;
    private readonly ConcurrentDictionary<string, SpectreLogger> _loggers = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreLoggerProvider"/> class.
    /// </summary>
    /// <param name="console">The console to which log entries are written.</param>
    public SpectreLoggerProvider(IAnsiConsole console)
    {
        Guard.IsNotNull(console);
        _console = console;
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        Guard.IsNotNull(categoryName);
        return _loggers.GetOrAdd(categoryName, name => new SpectreLogger(_console, name));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
