// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core;

/// <summary>
/// Severity levels for messages logged through <see cref="IBuildHost"/>.
/// Numerically higher values indicate higher severity, matching dotnet's logging vocabulary.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Lowest-detail messages, visible only at <c>diagnostic</c> verbosity.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Detailed diagnostic messages, visible at <c>detailed</c> verbosity and above.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Routine progress messages, visible at <c>normal</c> verbosity and above.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Non-fatal anomalies, visible at <c>minimal</c> verbosity and above.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Errors that do not by themselves stop the build, visible at every verbosity except suppressed.
    /// </summary>
    Error = 4,
}
