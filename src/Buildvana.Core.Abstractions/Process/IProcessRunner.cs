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
    /// <param name="workingDirectory">The working directory in which to run the process, or <see langword="null"/> to inherit the current process's working directory.</param>
    /// <param name="throwOnNonZero">If <see langword="true"/> (the default), a <see cref="BuildFailedException"/> is thrown when the process exits with a non-zero exit code; if <see langword="false"/>, the result is returned regardless of exit code.</param>
    /// <param name="onStdout">An optional callback invoked once per line of standard output as it is produced.
    /// The full output text is captured into the returned <see cref="ProcessResult"/> regardless.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the process.</param>
    /// <returns>A <see cref="ProcessResult"/> describing the process's outcome.</returns>
    /// <exception cref="BuildFailedException">The process exited with a non-zero exit code and <paramref name="throwOnNonZero"/> is <see langword="true"/>.</exception>
    Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        Action<string>? onStdout = null,
        CancellationToken cancellationToken = default);
}
