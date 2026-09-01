// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What the transitive override files are to hold at the end of a run.
/// </summary>
/// <remarks>
/// <para>A plan describes the whole repository, never one invocation's corner of it: the files reflect the
/// dependency graph, and the graph does not care which pins the command line named. This is also what makes
/// a stale override disappear on its own, with no pruning step of its own.</para>
/// </remarks>
internal sealed record TransitiveOverridePlan
{
    /// <summary>
    /// Gets the versions the central file is to state, for the packages the repository does not pin itself.
    /// </summary>
    public required IReadOnlyList<PackageOverride> Central { get; init; }

    /// <summary>
    /// Gets what each project's own file is to hold, one entry per project of the solution.
    /// </summary>
    public required IReadOnlyList<ProjectOverrides> Projects { get; init; }
}
