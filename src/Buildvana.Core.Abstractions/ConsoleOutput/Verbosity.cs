// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Controls how much of a reporter's output reaches the user. Each level enables all the
/// <see cref="MessageLevel"/>s enabled by the levels below it.
/// </summary>
/// <remarks>
/// <para>The members mirror <c>bv</c>'s <c>--verbosity</c> command-line vocabulary and are ordered from least to
/// most verbose. Which levels each one enables is stated by
/// <see cref="MessageLevelExtensions.MinimumVerbosity"/>: a message is shown when its level's minimum verbosity
/// is at most the one in effect.</para>
/// </remarks>
public enum Verbosity
{
    /// <summary>Only errors are shown.</summary>
    Quiet,

    /// <summary>Errors, warnings, and notices are shown. This is <c>bv</c>'s default, as it is the .NET CLI's.</summary>
    Minimal,

    /// <summary>Everything <see cref="Minimal"/> shows, plus informational messages.</summary>
    Normal,

    /// <summary>Everything <see cref="Normal"/> shows, plus detail messages.</summary>
    Detailed,

    /// <summary>Everything is shown, including trace messages.</summary>
    Diagnostic,
}
