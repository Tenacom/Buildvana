// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core;

/// <summary>
/// The canonical exception type raised when a Buildvana build step fails for a reason that carries
/// a process-style exit code (e.g. a tool invocation that exited non-zero).
/// </summary>
public sealed class BuildFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class with no message and an exit code of 1.
    /// </summary>
    public BuildFailedException()
        : this(1, "The build failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class with the specified message and an exit code of 1.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public BuildFailedException(string message)
        : this(1, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class with the specified message,
    /// inner exception, and an exit code of 1.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    /// <param name="innerException">The exception that caused this failure, if any.</param>
    public BuildFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
        ExitCode = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildFailedException"/> class with the specified exit code and message.
    /// </summary>
    /// <param name="exitCode">The exit code to surface to the host.</param>
    /// <param name="message">A message describing the failure.</param>
    public BuildFailedException(int exitCode, string message)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>
    /// Gets the exit code associated with this failure.
    /// </summary>
    public int ExitCode { get; }
}
