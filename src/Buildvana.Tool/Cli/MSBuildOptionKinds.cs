// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Identifies the kinds of MSBuild switches a <c>bv</c> command accepts and forwards to the underlying MSBuild invocation.
/// </summary>
[Flags]
internal enum MSBuildOptionKinds
{
    /// <summary>
    /// The command does not invoke MSBuild and forwards nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// The command forwards MSBuild property switches (<c>/p:Key=Value</c>).
    /// </summary>
    Properties = 0x1,

    /// <summary>
    /// The command forwards MSBuild switches other than properties (e.g. <c>/m:N</c>, <c>/v:m</c>).
    /// </summary>
    Switches = 0x2,

    /// <summary>
    /// The command forwards all MSBuild switches.
    /// </summary>
    All = Properties | Switches,
}
