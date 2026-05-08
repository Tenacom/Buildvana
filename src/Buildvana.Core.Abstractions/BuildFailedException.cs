// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Buildvana.Core;

/// <summary>
/// <para>The exception that Buildvana libraries throw to fail the build.</para>
/// <para>Hosts catch this exception at their top-level entry point and translate it
/// to whatever failure mechanism their runtime expects (e.g., <c>TaskLoggingHelper.LogError</c>,
/// a non-zero process exit code, etc.).</para>
/// </summary>
public sealed class BuildFailedException : Exception
{
    /// <summary>
    /// The default exit code, used when no exit code is specified.
    /// </summary>
    public const int DefaultExitCode = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class
    /// with <see cref="DefaultExitCode"/> and a generic message.
    /// </summary>
    public BuildFailedException()
        : this(DefaultExitCode, "The build failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class
    /// with <see cref="DefaultExitCode"/> and the specified <paramref name="message"/>.
    /// </summary>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    public BuildFailedException(string message)
        : this(DefaultExitCode, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class
    /// with <see cref="DefaultExitCode"/>, the specified <paramref name="message"/>,
    /// and the specified <paramref name="innerException"/>.
    /// </summary>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    /// <param name="innerException">The exception that caused the build to fail.</param>
    public BuildFailedException(string message, Exception innerException)
        : this(DefaultExitCode, message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class
    /// with the specified <paramref name="exitCode"/> and <paramref name="message"/>.
    /// </summary>
    /// <param name="exitCode">The exit code that should be surfaced to the host's runtime, where applicable.</param>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    public BuildFailedException(int exitCode, string message)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class
    /// with the specified <paramref name="exitCode"/>, <paramref name="message"/>,
    /// and <paramref name="innerException"/>.
    /// </summary>
    /// <param name="exitCode">The exit code that should be surfaced to the host's runtime, where applicable.</param>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    /// <param name="innerException">The exception that caused the build to fail.</param>
    public BuildFailedException(int exitCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    /// <summary>
    /// Gets the exit code that should be surfaced to the host's runtime, where applicable.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Throws a <see cref="BuildFailedException"/> with <see cref="DefaultExitCode"/>
    /// and the specified <paramref name="message"/> if <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition under which the build should fail.</param>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    public static void ThrowIf([DoesNotReturnIf(true)] bool condition, string message)
    {
        if (condition)
        {
            throw new BuildFailedException(message);
        }
    }

    /// <summary>
    /// Throws a <see cref="BuildFailedException"/> with <see cref="DefaultExitCode"/>
    /// and the specified <paramref name="message"/> if <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that, if violated, should cause the build to fail.</param>
    /// <param name="message">A message explaining the reason for failing the build.</param>
    public static void ThrowIfNot([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new BuildFailedException(message);
        }
    }

    /// <summary>
    /// Throws a <see cref="BuildFailedException"/> reporting that an unsupported property was accessed.
    /// </summary>
    /// <typeparam name="T">The expected return type of the property accessor.</typeparam>
    /// <param name="propertyName">The name of the unsupported property. Defaults to the calling member's name.</param>
    /// <param name="sourceFilePath">The source file where the access occurred. Defaults to the caller's file path.</param>
    /// <param name="sourceLineNumber">The source line where the access occurred. Defaults to the caller's line number.</param>
    /// <returns>This method never returns.</returns>
    [DoesNotReturn]
    public static T ThrowOnUnsupportedProperty<T>(
        [CallerMemberName] string propertyName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        => throw new BuildFailedException($"Unsupported property {propertyName} in {sourceFilePath} ({sourceLineNumber}).");

    /// <summary>
    /// Throws a <see cref="BuildFailedException"/> reporting that an unsupported method was called.
    /// </summary>
    /// <typeparam name="T">The expected return type of the method.</typeparam>
    /// <param name="methodName">The name of the unsupported method. Defaults to the calling member's name.</param>
    /// <param name="sourceFilePath">The source file where the call occurred. Defaults to the caller's file path.</param>
    /// <param name="sourceLineNumber">The source line where the call occurred. Defaults to the caller's line number.</param>
    /// <returns>This method never returns.</returns>
    [DoesNotReturn]
    public static T ThrowOnUnsupportedMethod<T>(
        [CallerMemberName] string methodName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        => throw new BuildFailedException($"Unsupported method {methodName} in {sourceFilePath} ({sourceLineNumber}).");
}
