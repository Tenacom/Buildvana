// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// An <see cref="IReporter"/> that writes to the process's standard output via <see cref="Console"/>.
/// </summary>
/// <remarks>
/// <para>Color is a function of message level, decided here and never by the caller: <c>error:</c> renders in
/// red and <c>warning:</c> in yellow (foreground only, no background fill); the remaining levels are uncolored
/// so they inherit the terminal's theme. The message body is never colored.</para>
/// <para>Output is serialized through an internal lock so that lines streamed from a child process's standard
/// output and standard error (which arrive on background threads) never interleave mid-line with each other or
/// with narration.</para>
/// </remarks>
public sealed partial class ConsoleReporter : IReporter
{
    private readonly Lock _writeLock = new();
    private readonly Stack<ActivityScope> _activityStack = new();
    private readonly bool _useColor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleReporter"/> class.
    /// </summary>
    /// <param name="verbosity">The verbosity that gates which message levels are rendered.</param>
    /// <param name="colorOverride">
    /// <see langword="true"/> to force color on, <see langword="false"/> to force it off, or
    /// <see langword="null"/> to auto-detect (color on unless output is redirected or the <c>NO_COLOR</c>
    /// environment variable is set). A non-<see langword="null"/> value wins over both, so <c>--color</c>
    /// overrides <c>NO_COLOR</c>.
    /// </param>
    public ConsoleReporter(Verbosity verbosity, bool? colorOverride)
    {
        Verbosity = verbosity;
        _useColor = colorOverride ?? (!IsNoColorSet() && !Console.IsOutputRedirected);
    }

    /// <inheritdoc/>
    public Verbosity Verbosity { get; }

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
                Console.WriteLine(FormatActivityLine(depth, title, elapsed: null));
            }

            return scope;
        }
    }

    /// <inheritdoc/>
    public void ChildOutput(string line)
    {
        Guard.IsNotNull(line);

        // Quiet swallows child output; the process runner's head/tail buffer still provides a failure tail.
        if (Verbosity == Verbosity.Quiet)
        {
            return;
        }

        lock (_writeLock)
        {
            Console.WriteLine(line);
        }
    }

    /// <inheritdoc/>
    public void ChildError(string line)
    {
        Guard.IsNotNull(line);

        // As with ChildOutput, Quiet swallows the live stream and relies on the head/tail failure tail.
        if (Verbosity == Verbosity.Quiet)
        {
            return;
        }

        lock (_writeLock)
        {
            Console.Error.WriteLine(line);
        }
    }

    /// <summary>
    /// Determines whether the <c>NO_COLOR</c> environment variable is set.
    /// </summary>
    /// <returns><see langword="true"/> if the <c>NO_COLOR</c> environment variable is set; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>The <c>NO_COLOR</c> environment variable is a widely-adopted convention for opting out of color in command-line applications.
    /// See <a href="https://no-color.org">https://no-color.org</a> for more information.</para>
    /// </remarks>
    private static bool IsNoColorSet() => Environment.GetEnvironmentVariable("NO_COLOR") is { Length: > 0 };

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
    private static string FormatActivityLine(int depth, string title, TimeSpan? elapsed)
        => elapsed is { } e
            ? string.Format(CultureInfo.InvariantCulture, "[{0}] {1}: done ({2:F1}s)", depth, title, e.TotalSeconds)
            : string.Format(CultureInfo.InvariantCulture, "[{0}] {1}: starting...", depth, title);

    private void WriteLeveledLine(MessageLevel level, string message)
    {
        var (color, word) = StyleFor(level);
        if (_useColor && color is { } foreground)
        {
            Console.ForegroundColor = foreground;
            Console.Write(word);
            Console.Write(':');
            Console.ResetColor();
        }
        else
        {
            Console.Write(word);
            Console.Write(':');
        }

        Console.Write(' ');
        Console.WriteLine(message);
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
                Console.WriteLine(FormatActivityLine(scope.Depth, scope.Title, scope.Elapsed));
            }
        }
    }
}
