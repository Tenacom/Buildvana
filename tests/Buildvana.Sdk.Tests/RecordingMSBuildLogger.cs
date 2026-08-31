// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

// Collects the errors of a build, so that a failing target test says what MSBuild complained about
// instead of only that the build failed.
internal sealed class RecordingMSBuildLogger : ILogger
{
    private readonly List<string> _errors = [];

    public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Quiet;

    public string? Parameters { get; set; }

    public IReadOnlyList<string> Errors => _errors;

    public void Initialize(IEventSource eventSource)
        => eventSource.ErrorRaised += (_, e) => _errors.Add($"{e.Code}: {e.Message}");

    public void Shutdown()
    {
    }
}
