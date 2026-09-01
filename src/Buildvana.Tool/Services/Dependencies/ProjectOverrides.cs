// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What one project's transitive override file is to hold.
/// </summary>
/// <remarks>
/// <para>Every project of the solution has one of these, and a project with nothing to promote has an empty
/// one: that is what tells the writer to remove a file a previous run left behind.</para>
/// </remarks>
internal sealed record ProjectOverrides
{
    /// <summary>Gets the full path of the project.</summary>
    public required string ProjectFullPath { get; init; }

    /// <summary>Gets the packages to promote to references of the project.</summary>
    public required IReadOnlyList<PackageOverride> Promotions { get; init; }
}
