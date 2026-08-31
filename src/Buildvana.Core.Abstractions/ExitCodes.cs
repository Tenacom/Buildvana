// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core;

/// <summary>
/// The exit codes Buildvana returns beyond the <c>0</c> of a run that did what it was asked.
/// </summary>
/// <remarks>
/// <para>These are the values a <see cref="BuildFailedException.ExitCode"/> may carry, and they mean the
/// same thing whichever command produced them. A host that surfaces exit codes returns them as they are;
/// a host that has no exit code to surface, such as an MSBuild task, ignores them.</para>
/// <para>A run that failed of its own accord exits with <see cref="BuildFailedException.DefaultExitCode"/>,
/// the exit code of a <see cref="BuildFailedException"/> that names none. A configuration file that cannot
/// be read or validated is such a failure, and so is a file Buildvana itself could not read or write: the
/// state of the repository, and of this process, is Buildvana's own business.</para>
/// </remarks>
public static class ExitCodes
{
    /// <summary>
    /// The exit code of an invocation refused before the command ran: an unknown command, subcommand, or
    /// option; an argument too many or too few; an option value that does not parse.
    /// </summary>
    public const int Usage = 2;

    /// <summary>
    /// The exit code of a run stopped by a program Buildvana invoked: a child process that failed, or one
    /// that succeeded and produced output Buildvana cannot use. Warnings say which program, and nothing
    /// after it runs.
    /// </summary>
    /// <remarks>
    /// <para>The child's own exit code is not returned in its place. It says what a program Buildvana does
    /// not own makes of a failure, which collides with the meanings stated here, and the message reports it
    /// anyway.</para>
    /// </remarks>
    public const int ExternalProgramFailed = 3;

    /// <summary>
    /// The exit code of a run terminated with Ctrl-C: 128 + SIGINT (2), the POSIX convention for a process
    /// terminated by a signal.
    /// </summary>
    public const int Cancelled = 130;
}
