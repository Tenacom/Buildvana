// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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

    /// <summary>
    /// <para>Fails the build because an unsupported method has been called.</para>
    /// <para>This method does not return.</para>
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="methodName">The name of the unsupported method. This parameter defaults to the name of the calling method.</param>
    /// <param name="sourceFilePath">The path of the source file where the unsupported method is called. This parameter defaults to the caller's file path.</param>
    /// <param name="sourceLineNumber">The line number in the source file where the unsupported method is called. This parameter defaults to the caller's line number.</param>
    [DoesNotReturn]
    public static void FailOnUnsupportedMethod(this IBuildHost host, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        => host.Fail($"Unsupported method {methodName} in {sourceFilePath} ({sourceLineNumber})");

    /// <summary>
    /// <para>Fails the build because an unsupported method has been called.</para>
    /// <para>This method does not return.</para>
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="host">The build host.</param>
    /// <param name="methodName">The name of the unsupported method. This parameter defaults to the name of the calling method.</param>
    /// <param name="sourceFilePath">The path of the source file where the unsupported method is called. This parameter defaults to the caller's file path.</param>
    /// <param name="sourceLineNumber">The line number in the source file where the unsupported method is called. This parameter defaults to the caller's line number.</param>
    /// <returns>This method never returns.</returns>
    [DoesNotReturn]
    public static T FailOnUnsupportedMethod<T>(this IBuildHost host, [CallerMemberName] string methodName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        => host.Fail<T>($"Unsupported method {methodName} in {sourceFilePath} ({sourceLineNumber})");

    /// <summary>
    /// <para>Fails the build because an unsupported property setter has been called.</para>
    /// <para>This method does not return.</para>
    /// </summary>
    /// <param name="host">The build host.</param>
    /// <param name="propertyName">The name of the unsupported property. This parameter defaults to the name of the calling property (or method).</param>
    /// <param name="sourceFilePath">The path of the source file where the unsupported property is accessed. This parameter defaults to the caller's file path.</param>
    /// <param name="sourceLineNumber">The line number in the source file where the unsupported property is accessed. This parameter defaults to the caller's line number.</param>
    [DoesNotReturn]
    public static void FailOnUnsupportedProperty(this IBuildHost host, [CallerMemberName] string propertyName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        => host.Fail($"Unsupported property {propertyName} in {sourceFilePath} ({sourceLineNumber})");

    /// <summary>
    /// <para>Fails the build because an unsupported property getter has been called.</para>
    /// <para>This method does not return.</para>
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="host">The build host.</param>
    /// <param name="propertyName">The name of the unsupported property. This parameter defaults to the name of the calling property (or method).</param>
    /// <param name="sourceFilePath">The path of the source file where the unsupported property is accessed. This parameter defaults to the caller's file path.</param>
    /// <param name="sourceLineNumber">The line number in the source file where the unsupported property is accessed. This parameter defaults to the caller's line number.</param>
    /// <returns>This method never returns.</returns>
    [DoesNotReturn]
    public static T FailOnUnsupportedProperty<T>(this IBuildHost host, [CallerMemberName] string propertyName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        => host.Fail<T>($"Unsupported property {propertyName} in {sourceFilePath} ({sourceLineNumber})");
}
