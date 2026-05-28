// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Controls how much of a reporter's output reaches the user. Each level enables all the
/// <see cref="MessageLevel"/>s enabled by the levels below it (see <see cref="MessageLevel"/> for the mapping).
/// </summary>
/// <remarks>
/// The members mirror <c>bv</c>'s <c>--verbosity</c> command-line vocabulary and are ordered from least to most
/// verbose, so a message at a given <see cref="MessageLevel"/> is shown when
/// <c>(int)level &lt;= (int)verbosity</c>.
/// </remarks>
public enum Verbosity
{
    /// <summary>Only errors are shown.</summary>
    Quiet,

    /// <summary>Errors and warnings are shown.</summary>
    Minimal,

    /// <summary>Errors, warnings, and informational messages are shown. This is the default.</summary>
    Normal,

    /// <summary>Everything <see cref="Normal"/> shows, plus detail messages.</summary>
    Detailed,

    /// <summary>Everything is shown, including trace messages.</summary>
    Diagnostic,
}
