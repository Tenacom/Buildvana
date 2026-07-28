// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using Microsoft.Build.Framework;

/// <summary>
/// An <see cref="IBuildEngine"/> that records logged events for assertion.
/// </summary>
internal class RecordingBuildEngine : IBuildEngine
{
    private readonly List<BuildErrorEventArgs> _errors = [];

    private readonly List<BuildWarningEventArgs> _warnings = [];

    private readonly List<BuildMessageEventArgs> _messages = [];

    private readonly List<CustomBuildEventArgs> _customEvents = [];

    public IReadOnlyList<BuildErrorEventArgs> Errors => _errors;

    public IReadOnlyList<BuildWarningEventArgs> Warnings => _warnings;

    public IReadOnlyList<BuildMessageEventArgs> Messages => _messages;

    public IReadOnlyList<CustomBuildEventArgs> CustomEvents => _customEvents;

    public bool ContinueOnError => false;

    public int LineNumberOfTaskNode => 0;

    public int ColumnNumberOfTaskNode => 0;

    public string ProjectFileOfTaskNode => string.Empty;

    public void LogErrorEvent(BuildErrorEventArgs? e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _errors.Add(e);
    }

    public void LogWarningEvent(BuildWarningEventArgs? e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _warnings.Add(e);
    }

    public void LogMessageEvent(BuildMessageEventArgs? e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _messages.Add(e);
    }

    public void LogCustomEvent(CustomBuildEventArgs? e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _customEvents.Add(e);
    }

    public bool BuildProjectFile(
        string? projectFileName,
        string[]? targetNames,
        IDictionary? globalProperties,
        IDictionary? targetOutputs)
        => throw new NotSupportedException();
}
