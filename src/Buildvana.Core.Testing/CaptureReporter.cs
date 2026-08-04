// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Buildvana.Core.ConsoleOutput;

namespace Buildvana.Core.Testing;

/// <summary>
/// A capture fake for <see cref="IReporter"/>: records everything, renders nothing. Its <see cref="Verbosity"/>
/// is fixed at the maximum, so even gating helpers that consult it before reporting (e.g. the
/// <c>CompositeFormat</c>-based overloads in <c>ReporterExtensions</c>) never drop a message, and tests
/// can assert on messages of any level.
/// </summary>
public sealed class CaptureReporter : IReporter
{
    private readonly List<(MessageLevel Level, string Message)> _messages = [];
    private readonly List<string> _activityTitles = [];
    private readonly List<string> _childOutputLines = [];
    private readonly List<string> _childErrorLines = [];

    /// <summary>
    /// Gets the messages reported via <see cref="Report"/>, in order.
    /// </summary>
    public IReadOnlyList<(MessageLevel Level, string Message)> Messages => _messages;

    /// <summary>
    /// Gets the titles of the activities begun via <see cref="BeginActivity"/>, in order.
    /// </summary>
    public IReadOnlyList<string> ActivityTitles => _activityTitles;

    /// <summary>
    /// Gets the lines written via <see cref="ChildOutput"/>, in order.
    /// </summary>
    public IReadOnlyList<string> ChildOutputLines => _childOutputLines;

    /// <summary>
    /// Gets the lines written via <see cref="ChildError"/>, in order.
    /// </summary>
    public IReadOnlyList<string> ChildErrorLines => _childErrorLines;

    /// <inheritdoc/>
    public Verbosity Verbosity => Verbosity.Diagnostic;

    /// <inheritdoc/>
    public void Report(MessageLevel level, string message) => _messages.Add((level, message));

    /// <inheritdoc/>
    public IActivityScope BeginActivity(string title)
    {
        _activityTitles.Add(title);
        return NullActivityScope.Instance;
    }

    /// <inheritdoc/>
    public void ChildOutput(string line, Verbosity? minimumVerbosity) => _childOutputLines.Add(line);

    /// <inheritdoc/>
    public void ChildError(string line, Verbosity? minimumVerbosity) => _childErrorLines.Add(line);
}
