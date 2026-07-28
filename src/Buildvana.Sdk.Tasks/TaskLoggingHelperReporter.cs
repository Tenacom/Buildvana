// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Buildvana.Core.ConsoleOutput;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Buildvana.Sdk;

/// <summary>
/// An <see cref="IReporter"/> that forwards reported messages to MSBuild's
/// <see cref="TaskLoggingHelper"/>, so that Core services hosted inside a task surface their
/// human-facing output through the build's own loggers.
/// </summary>
/// <remarks>
/// <para>Message levels map to MSBuild severities as follows: <see cref="MessageLevel.Error"/> and
/// <see cref="MessageLevel.Warning"/> become build errors and warnings; <see cref="MessageLevel.Info"/>,
/// <see cref="MessageLevel.Detail"/>, and <see cref="MessageLevel.Trace"/> become messages of
/// <see cref="MessageImportance.High"/>, <see cref="MessageImportance.Normal"/>, and
/// <see cref="MessageImportance.Low"/> importance respectively.</para>
/// <para>Messages are always forwarded, regardless of <see cref="Verbosity"/>: visibility is governed by
/// MSBuild's own verbosity and importance filtering, exactly like any other message logged by a task.
/// <see cref="Verbosity"/> is derived from <see cref="IBuildEngine10.EngineServices"/> when available
/// (falling back to fully permissive otherwise), so that callers checking it — such as the formatting
/// helpers in <see cref="ReporterExtensions"/> — skip work whose output MSBuild would discard anyway.</para>
/// <para>Activity start and outcome lines are logged at <see cref="MessageImportance.Normal"/> importance,
/// so they are hidden at MSBuild's default (minimal) verbosity and visible from normal verbosity up.</para>
/// <para>Child-process output and error lines are both forwarded as low-importance messages: MSBuild has no
/// neutral standard-error channel, and logging a build error or warning would misrepresent — and, given how
/// task success is determined, potentially fail the build over — stderr lines that many tools use for
/// progress and hints.</para>
/// </remarks>
internal sealed partial class TaskLoggingHelperReporter : IReporter
{
    private readonly TaskLoggingHelper _log;
    private readonly EngineServices? _engineServices;
    private readonly Lock _activityLock = new();
    private readonly Stack<ActivityScope> _activityStack = new();

    public TaskLoggingHelperReporter(TaskLoggingHelper log, IBuildEngine engine)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(engine);
        _log = log;
        _engineServices = (engine as IBuildEngine10)?.EngineServices;
    }

    public Verbosity Verbosity => LogsMessagesOfImportance(MessageImportance.Low) ? Verbosity.Diagnostic
        : LogsMessagesOfImportance(MessageImportance.Normal) ? Verbosity.Detailed
        : LogsMessagesOfImportance(MessageImportance.High) ? Verbosity.Normal
        : Verbosity.Minimal;

    public void Report(MessageLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        switch (level)
        {
            case MessageLevel.Error:
                _log.LogError("{0}", message);
                break;
            case MessageLevel.Warning:
                _log.LogWarning("{0}", message);
                break;
            case MessageLevel.Info:
                _log.LogMessage(MessageImportance.High, "{0}", message);
                break;
            case MessageLevel.Detail:
                _log.LogMessage(MessageImportance.Normal, "{0}", message);
                break;
            case MessageLevel.Trace:
                _log.LogMessage(MessageImportance.Low, "{0}", message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown message level.");
        }
    }

    public IActivityScope BeginActivity(string title)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        lock (_activityLock)
        {
            var depth = _activityStack.Count + 1;
            var scope = new ActivityScope(this, title, depth);
            _activityStack.Push(scope);
            _log.LogMessage(
                MessageImportance.Normal,
                "{0}",
                FormatActivityLine(depth, title, elapsed: null, outcomeMessage: null));
            return scope;
        }
    }

    public void ChildOutput(string line, Verbosity? minimumVerbosity)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (minimumVerbosity is { } v && !this.IsVerbosityAtLeast(v))
        {
            return;
        }

        _log.LogMessage(MessageImportance.Low, "{0}", line);
    }

    public void ChildError(string line, Verbosity? minimumVerbosity)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (minimumVerbosity is { } v && !this.IsVerbosityAtLeast(v))
        {
            return;
        }

        _log.LogMessage(MessageImportance.Low, "{0}", line);
    }

    // Same line format as ConsoleReporter, so activities look the same whether a service runs under `bv` or MSBuild.
    private static string FormatActivityLine(int depth, string title, TimeSpan? elapsed, string? outcomeMessage)
        => elapsed is { } e
            ? string.Format(
                CultureInfo.InvariantCulture,
                "[{0}] {1}: done ({2:F1}s){3}{4}",
                depth,
                title,
                e.TotalSeconds,
                outcomeMessage is null ? string.Empty : " - ",
                outcomeMessage)
            : string.Format(CultureInfo.InvariantCulture, "[{0}] {1}: starting...", depth, title);

    private bool LogsMessagesOfImportance(MessageImportance importance)
        => _engineServices?.LogsMessagesOfImportance(importance) ?? true;

    private void EndActivity(ActivityScope scope, bool completed)
    {
        lock (_activityLock)
        {
            if (_activityStack.Count > 0 && ReferenceEquals(_activityStack.Peek(), scope))
            {
                _activityStack.Pop();
            }

            // No outcome line unless the activity was explicitly completed (e.g. the work threw before Complete).
            if (completed)
            {
                _log.LogMessage(
                    MessageImportance.Normal,
                    "{0}",
                    FormatActivityLine(scope.Depth, scope.Title, scope.Elapsed, scope.OutcomeMessage));
            }
        }
    }
}
