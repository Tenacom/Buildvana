// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// An <see cref="IReporter"/> that renders to a pair of <see cref="TextWriter"/>s supplied by a derived class.
/// </summary>
/// <remarks>
/// <para>This type owns everything about how output looks — level labels, color policy, activity nesting,
/// serialization — and nothing about where it goes: a derived class binds <see cref="OutputWriter"/> and
/// <see cref="ErrorWriter"/> to concrete writers (see <see cref="ConsoleReporter"/> for the process's standard
/// streams).</para>
/// <para>Diagnostics (leveled messages and activity header/outcome lines) go to <see cref="ErrorWriter"/>, so
/// that <see cref="OutputWriter"/> carries only command deliverables and child-process standard output
/// (<see cref="ChildOutput"/>) and stays pipeable at any verbosity, per the prevailing CLI convention.</para>
/// <para>Color is a function of message level, decided here and never by the caller: <c>error:</c> renders in
/// red and <c>warning:</c> in yellow (foreground only, no background fill); the remaining levels are uncolored
/// so they inherit the terminal's theme. The message body is never colored. Coloring uses
/// <see cref="AnsiEscapes"/> rather than <see cref="Console.ForegroundColor"/>, because the latter is tied to
/// the process's standard output and would corrupt a redirected deliverable stream. Whether color is used at
/// all is not decided here: it is a property of the destination, so a derived class settles it and passes the
/// answer to this constructor.</para>
/// <para>Output is serialized through an internal lock so that lines streamed from a child process's standard
/// output and standard error (which arrive on background threads) never interleave mid-line with each other or
/// with narration.</para>
/// </remarks>
/// <param name="verbosity">The verbosity that gates which message levels are rendered.</param>
/// <param name="useColor">
/// <see langword="true"/> to wrap colored levels' labels in ANSI escape sequences, <see langword="false"/> to
/// write them verbatim.
/// </param>
public abstract partial class TextWriterReporter(Verbosity verbosity, bool useColor) : IReporter
{
    private readonly Lock _writeLock = new();
    private readonly Stack<ActivityScope> _activityStack = new();

    /// <inheritdoc/>
    public Verbosity Verbosity { get; } = verbosity;

    /// <summary>
    /// Gets the writer that receives command deliverables and child-process standard output.
    /// </summary>
    /// <remarks>
    /// <para>Resolved anew on every write, so an implementation may return a writer that changes over time.</para>
    /// </remarks>
    protected abstract TextWriter OutputWriter { get; }

    /// <summary>
    /// Gets the writer that receives diagnostics and child-process standard error.
    /// </summary>
    /// <remarks>
    /// <para>Resolved anew on every write, so an implementation may return a writer that changes over time.</para>
    /// </remarks>
    protected abstract TextWriter ErrorWriter { get; }

    /// <inheritdoc/>
    public void Report(MessageLevel level, string message)
    {
        Guard.IsNotNull(message);
        if (!this.IsEnabled(level))
        {
            return;
        }

        lock (_writeLock)
        {
            WriteLeveledLine(level, message);
        }
    }

    /// <inheritdoc/>
    public IActivityScope BeginActivity(string title)
    {
        Guard.IsNotNullOrEmpty(title);
        lock (_writeLock)
        {
            var depth = _activityStack.Count + 1;
            var scope = new ActivityScope(this, title, depth);
            _activityStack.Push(scope);
            if (this.IsEnabled(MessageLevel.Info))
            {
                ErrorWriter.WriteLine(FormatActivityLine(depth, title, elapsed: null, outcomeMessage: null));
            }

            return scope;
        }
    }

    /// <inheritdoc/>
    public void ChildOutput(string line, Verbosity? minimumVerbosity)
    {
        Guard.IsNotNull(line);

        // If a minimum verbosity is specified, respect it; otherwise, write regardless of current verbosity.
        // This is useful for child processes whose verbosity we control, e.g., `dotnet`.
        if (minimumVerbosity is { } v && !this.IsVerbosityAtLeast(v))
        {
            return;
        }

        lock (_writeLock)
        {
            OutputWriter.WriteLine(line);
        }
    }

    /// <inheritdoc/>
    public void ChildError(string line, Verbosity? minimumVerbosity)
    {
        Guard.IsNotNull(line);

        // If a minimum verbosity is specified, respect it; otherwise, write regardless of current verbosity.
        // This is useful for child processes whose verbosity we control, e.g., `dotnet`.
        if (minimumVerbosity is { } v && !this.IsVerbosityAtLeast(v))
        {
            return;
        }

        lock (_writeLock)
        {
            ErrorWriter.WriteLine(line);
        }
    }

    private static (ConsoleColor? Color, string Word) StyleFor(MessageLevel level) => level switch
    {
        MessageLevel.Error => (ConsoleColor.Red, "error"),
        MessageLevel.Warning => (ConsoleColor.Yellow, "warning"),
        MessageLevel.Info => (null, "info"),
        MessageLevel.Detail => (null, "detail"),
        MessageLevel.Trace => (null, "trace"),
        _ => ThrowHelper.ThrowArgumentOutOfRangeException<(ConsoleColor?, string)>(nameof(level), level, "Unknown message level."),
    };

    // Activity header/outcome lines are label-less; the leading "[depth]" conveys nesting without indentation.
    private static string FormatActivityLine(int depth, string title, TimeSpan? elapsed, string? outcomeMessage)
    {
        if (elapsed is null)
        {
            return $"[{depth}] {title}: starting...";
        }

        var elapsedStr = elapsed.Value.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        var separator = outcomeMessage is null ? string.Empty : " - ";
        return $"[{depth}] {title}: done ({elapsedStr}s){separator}{outcomeMessage}";
    }

    // A single WriteLine per message: the error writer is expected to auto-flush, so composing first keeps the
    // line atomic at the OS level (the lock only serializes this reporter, not the logo or Spectre's stdout
    // writes) and costs one write syscall instead of up to five.
    private void WriteLeveledLine(MessageLevel level, string message)
    {
        var (color, word) = StyleFor(level);
        var line = useColor && color is { } foreground
            ? $"{AnsiEscapes.Foreground(foreground)}{word}:{AnsiEscapes.Reset} {message}"
            : $"{word}: {message}";
        ErrorWriter.WriteLine(line);
    }

    private void EndActivity(ActivityScope scope, bool completed)
    {
        lock (_writeLock)
        {
            if (_activityStack.Count > 0 && ReferenceEquals(_activityStack.Peek(), scope))
            {
                _activityStack.Pop();
            }

            // No outcome line unless the activity was explicitly completed (e.g. the work threw before Complete).
            if (completed && this.IsEnabled(MessageLevel.Info))
            {
                ErrorWriter.WriteLine(FormatActivityLine(scope.Depth, scope.Title, scope.Elapsed, scope.OutcomeMessage));
            }
        }
    }
}
