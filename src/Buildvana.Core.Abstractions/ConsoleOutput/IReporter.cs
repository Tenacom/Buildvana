// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Reports human-facing console output: leveled messages, activity grouping, and verbatim child-process
/// passthrough. This is the primitive surface; formatting and per-level convenience helpers are provided as
/// extension methods (see <see cref="ReporterExtensions"/>).
/// </summary>
public interface IReporter
{
    /// <summary>
    /// Gets the verbosity that gates which <see cref="MessageLevel"/>s are rendered.
    /// </summary>
    Verbosity Verbosity { get; }

    /// <summary>
    /// Reports a single message at the given <paramref name="level"/>. The message is rendered only when
    /// <paramref name="level"/> is enabled by the current <see cref="Verbosity"/>.
    /// </summary>
    /// <param name="level">The severity of the message.</param>
    /// <param name="message">The message text.</param>
    void Report(MessageLevel level, string message);

    /// <summary>
    /// Begins an activity: a header is rendered now, and the returned scope renders the outcome (and elapsed
    /// time) when disposed.
    /// </summary>
    /// <param name="title">A short description of the work the activity covers.</param>
    /// <returns>A scope that closes the activity when disposed.</returns>
    IActivityScope BeginActivity(string title);

    /// <summary>
    /// Writes a line from a child process's standard output verbatim: no level label, no color, no category.
    /// Used to stream a spawned process's standard output through to this process's standard output.
    /// </summary>
    /// <param name="line">The line of child-process standard output to write.</param>
    /// <param name="verbosity">The verbosity level at which the line should be written.
    /// If `null`, the line is written regardless of the current verbosity.</param>
    void ChildOutput(string line, Verbosity? verbosity);

    /// <summary>
    /// Writes a line from a child process's standard error verbatim: no level label, no color, no category.
    /// Used to stream a spawned process's standard error through to this process's standard error.
    /// </summary>
    /// <param name="line">The line of child-process standard error to write.</param>
    /// <param name="verbosity">The verbosity level at which the line should be written.
    /// If `null`, the line is written regardless of the current verbosity.</param>
    void ChildError(string line, Verbosity? verbosity);
}
