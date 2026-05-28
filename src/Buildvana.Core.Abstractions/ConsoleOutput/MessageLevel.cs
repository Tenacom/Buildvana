// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// The severity of a message reported through an <see cref="IReporter"/>. The level both classifies the
/// message and, together with the reporter's <see cref="Verbosity"/>, decides whether it is shown.
/// </summary>
/// <remarks>
/// Members are ordered from highest to lowest severity, mapping one-to-one onto the <see cref="Verbosity"/>
/// thresholds: <see cref="Error"/>↔<see cref="Verbosity.Quiet"/>, <see cref="Warning"/>↔<see cref="Verbosity.Minimal"/>,
/// <see cref="Info"/>↔<see cref="Verbosity.Normal"/>, <see cref="Detail"/>↔<see cref="Verbosity.Detailed"/>,
/// <see cref="Trace"/>↔<see cref="Verbosity.Diagnostic"/>.
/// </remarks>
public enum MessageLevel
{
    /// <summary>An error: something went wrong. Shown at every verbosity.</summary>
    Error,

    /// <summary>A warning: something looks off but is not fatal.</summary>
    Warning,

    /// <summary>An informational milestone. Shown at <see cref="Verbosity.Normal"/> and above.</summary>
    Info,

    /// <summary>A detail useful when following along closely. Shown at <see cref="Verbosity.Detailed"/> and above.</summary>
    Detail,

    /// <summary>Fine-grained diagnostic chatter. Shown only at <see cref="Verbosity.Diagnostic"/>.</summary>
    Trace,
}
