// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Buildvana.Core;

/// <summary>
/// Provides default extension methods for <see cref="IBuildHost"/>, derived from its primary members.
/// </summary>
public static class BuildHostExtensions
{
    /// <summary>
    /// <para>Fails the build with the specified message.</para>
    /// <para>This method does not return.</para>
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="host">The build host.</param>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    /// <returns>This method never returns.</returns>
    [DoesNotReturn]
    public static T Fail<T>(this IBuildHost host, string message)
    {
        host.Fail(message);
        throw new UnreachableException();
    }

    /// <summary>
    /// Fails the build with the specified message if a condition is not verified.
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    public static void Ensure(this IBuildHost host, [DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            host.Fail(message);
        }
    }

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Trace"/> level.
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="message">The message to log.</param>
    public static void LogTrace(this IBuildHost host, string message) => host.Log(LogLevel.Trace, message);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Debug"/> level.
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="message">The message to log.</param>
    public static void LogDebug(this IBuildHost host, string message) => host.Log(LogLevel.Debug, message);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Information"/> level.
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="message">The message to log.</param>
    public static void LogInformation(this IBuildHost host, string message) => host.Log(LogLevel.Information, message);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Warning"/> level.
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="message">The message to log.</param>
    public static void LogWarning(this IBuildHost host, string message) => host.Log(LogLevel.Warning, message);

    /// <summary>
    /// Logs a message at <see cref="LogLevel.Error"/> level.
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="message">The message to log.</param>
    public static void LogError(this IBuildHost host, string message) => host.Log(LogLevel.Error, message);
}
