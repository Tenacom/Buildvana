// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Buildvana.Core.Process;

/// <summary>
/// Runs an external process and reports its outcome through a <see cref="ProcessResult"/>.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs an external process to completion, capturing its standard output and standard error.
    /// </summary>
    /// <param name="executable">The path to (or name of) the executable to run.</param>
    /// <param name="args">The arguments to pass to <paramref name="executable"/>.</param>
    /// <param name="environment">Environment variables to apply on top of the inherited environment: each entry adds or
    /// overrides a variable, and a <see langword="null"/> value removes that variable from the child process. Pass
    /// <see langword="null"/> to run with the current process's environment unchanged.</param>
    /// <param name="workingDirectory">The working directory in which to run the process, or <see langword="null"/>
    /// to inherit the current process's working directory.</param>
    /// <param name="throwOnNonZero">If <see langword="true"/> (the default), a <see cref="BuildFailedException"/> is thrown when
    /// the process exits with a non-zero exit code; if <see langword="false"/>, the result is returned regardless of exit code.</param>
    /// <param name="onStdout">An optional callback invoked once per line of standard output as it is produced.
    /// The full output text is captured into the returned <see cref="ProcessResult"/> regardless.</param>
    /// <param name="onStderr">An optional callback invoked once per line of standard error as it is produced.
    /// The full error text is captured into the returned <see cref="ProcessResult"/> regardless.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the process.</param>
    /// <returns>A <see cref="ProcessResult"/> describing the process's outcome.</returns>
    Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an external process to completion with standard input, output, and error inherited from the current
    /// process, and returns its exit code.
    /// </summary>
    /// <remarks>
    /// <para>The child shares the current process's console: it sees the same TTY (so its own color and
    /// interactivity detection behave as if it were launched directly), reads the same standard input, and writes
    /// to the same standard output and error. Nothing is captured or redirected, which is why this method reports
    /// the bare exit code rather than a <see cref="ProcessResult"/>, and never throws on a non-zero exit code:
    /// the child has already told the user everything there is to tell, and the caller is expected to forward
    /// the exit code.</para>
    /// <para>A Ctrl-C reaches the child directly through the shared console; the current process suppresses its
    /// own termination for the duration of the run, so that the child can shut down on its own terms and its
    /// exit code is still observed and returned.</para>
    /// </remarks>
    /// <param name="executable">The path to (or name of) the executable to run.</param>
    /// <param name="args">The arguments to pass to <paramref name="executable"/>.</param>
    /// <param name="environment">Environment variables to apply on top of the inherited environment: each entry adds or
    /// overrides a variable, and a <see langword="null"/> value removes that variable from the child process. Pass
    /// <see langword="null"/> to run with the current process's environment unchanged.</param>
    /// <param name="workingDirectory">The working directory in which to run the process, or <see langword="null"/>
    /// to inherit the current process's working directory.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that, when signalled, kills the process
    /// (and its process tree) and cancels the wait.</param>
    /// <returns>The process's exit code.</returns>
    /// <exception cref="BuildFailedException">The process could not be started (e.g. the executable was not found).</exception>
    Task<int> RunWithInheritedStdioAsync(
        string executable,
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}
