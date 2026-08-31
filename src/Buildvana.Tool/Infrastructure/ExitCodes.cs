// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;

namespace Buildvana.Tool.Infrastructure;

/// <summary>
/// The exit codes <c>bv</c> returns beyond the <c>0</c> of a run that did what it was asked.
/// </summary>
/// <remarks>
/// <para>A command that ran and failed exits with <see cref="BuildFailedException.DefaultExitCode"/>, the
/// exit code of a <see cref="BuildFailedException"/> that names none. A configuration file <c>bv</c> cannot
/// read or validate is such a failure: the file is the repository's state, not the invocation's shape.</para>
/// </remarks>
internal static class ExitCodes
{
    /// <summary>
    /// The exit code of an invocation <c>bv</c> refused before running the command: an unknown command,
    /// subcommand, or option; an argument too many or too few; an option value that does not parse.
    /// </summary>
    public const int Usage = 2;

    /// <summary>
    /// The exit code of a run whose step could not complete: a source that cannot be reached, a child
    /// process that failed, a file that could not be read. Warnings say which step, and nothing after it
    /// runs. The dependency commands are the ones that tell this apart from a run that completed and found
    /// something wrong.
    /// </summary>
    public const int StepFailed = 3;

    /// <summary>
    /// The exit code of a run terminated with Ctrl-C: 128 + SIGINT (2), the POSIX convention for a process
    /// terminated by a signal.
    /// </summary>
    public const int Cancelled = 130;
}
