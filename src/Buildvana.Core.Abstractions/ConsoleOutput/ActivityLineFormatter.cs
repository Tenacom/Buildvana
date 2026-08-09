// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Formats the lines that mark the beginning and the successful end of an activity.
/// </summary>
/// <remarks>
/// <para>Activity lines are formatted here rather than by each <see cref="IReporter"/> implementation, so that
/// activities look the same whichever reporter renders them — a service's narration reads the same whether it
/// runs under <c>bv</c> or inside an MSBuild task.</para>
/// <para>The lines carry no level label: the leading <c>[depth]</c> conveys nesting without indentation.</para>
/// </remarks>
public static class ActivityLineFormatter
{
    /// <summary>
    /// Formats the line that marks the beginning of an activity.
    /// </summary>
    /// <param name="depth">The nesting depth of the activity, where 1 is a top-level activity.</param>
    /// <param name="title">The title of the activity.</param>
    /// <returns>The formatted line.</returns>
    public static string FormatStart(int depth, string title) => $"[{depth}] {title}: starting...";

    /// <summary>
    /// Formats the line that marks the successful completion of an activity.
    /// </summary>
    /// <param name="depth">The nesting depth of the activity, where 1 is a top-level activity.</param>
    /// <param name="title">The title of the activity.</param>
    /// <param name="elapsed">The time the activity took.</param>
    /// <param name="outcomeMessage">An optional message describing the outcome of the activity.</param>
    /// <returns>The formatted line.</returns>
    public static string FormatOutcome(int depth, string title, TimeSpan elapsed, string? outcomeMessage)
    {
        var seconds = elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        var separator = outcomeMessage is null ? string.Empty : " - ";
        return $"[{depth}] {title}: done ({seconds}s){separator}{outcomeMessage}";
    }
}
