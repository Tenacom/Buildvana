// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Common;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One entry of the log a restore leaves in a project's assets file.
/// </summary>
/// <param name="Code">The code NuGet gave the entry.</param>
/// <param name="Level">The level NuGet gave the entry.</param>
/// <param name="LibraryId">The package the entry is about, empty where it is about none.</param>
/// <param name="Message">What NuGet wrote about it.</param>
/// <param name="TargetGraphs">The target graphs the entry concerns, empty where it concerns the project as
/// a whole.</param>
/// <remarks>
/// <para>The audit codes NU1901 to NU1904 name a package a security advisory covers, one code per severity.
/// Two further codes steer a run of the override lifecycle: NU1900 says that a package source could not be
/// read in full, which may be the vulnerability data the audit needs, and NU1905 that an audit source
/// provided none.</para>
/// <para>An entry whose level is an error and whose code is none of the audit ones says that the restore
/// failed for a reason of its own, which the lifecycle reports as a failed step.</para>
/// </remarks>
internal sealed record AssetsLogEntry(
    NuGetLogCode Code,
    LogLevel Level,
    string LibraryId,
    string Message,
    IReadOnlyList<string> TargetGraphs);
