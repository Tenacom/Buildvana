// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Buildvana.Core;

/// <summary>
/// <para>The contract by which any reusable Buildvana library reports failures and log output to its host.</para>
/// <para>Implementations adapt host runtimes (Cake, MSBuild tasks, etc.) so that libraries built on top of this
/// interface remain host-agnostic.</para>
/// </summary>
/// <remarks>
/// <para>Implementations of <see cref="Fail(string)"/> must throw an exception appropriate to the host.
/// The thrown exception's message is guaranteed to end up in the host's log exactly once, through whichever
/// path the host considers canonical.</para>
/// </remarks>
public interface IBuildHost
{
    /// <summary>
    /// <para>Fails the build with the specified message.</para>
    /// <para>This method does not return.</para>
    /// </summary>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    [DoesNotReturn]
    void Fail(string message);

    /// <summary>
    /// Returns a value indicating whether log entries at the specified <paramref name="level"/> are emitted.
    /// </summary>
    /// <param name="level">The severity level to check.</param>
    /// <returns><see langword="true"/> if entries at <paramref name="level"/> would be emitted; <see langword="false"/> otherwise.</returns>
    bool IsEnabled(LogLevel level);

    /// <summary>
    /// Logs a message at the specified severity <paramref name="level"/>.
    /// </summary>
    /// <param name="level">The severity level to log at.</param>
    /// <param name="message">The message to log.</param>
    void Log(LogLevel level, string message);
}
