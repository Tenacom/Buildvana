// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What one project's assets file says: the graph a restore resolved for it, what the project itself
/// references, and what the restore logged.
/// </summary>
/// <remarks>
/// <para>This is NuGet's own verdict on the project, read and not recomputed. The override lifecycle judges
/// the restore by this content rather than by its exit code, because a restore whose audit findings are
/// errors fails while still writing everything here.</para>
/// </remarks>
internal sealed record ProjectAssets
{
    /// <summary>Gets the full path of the project the assets file belongs to.</summary>
    public required string ProjectFullPath { get; init; }

    /// <summary>Gets every package the restore resolved, per target graph.</summary>
    public required IReadOnlyList<ResolvedPackage> Packages { get; init; }

    /// <summary>
    /// Gets the ids of the packages the project references directly, in no particular order and without
    /// repetition.
    /// </summary>
    public required IReadOnlyList<string> DirectReferences { get; init; }

    /// <summary>
    /// Gets a value indicating whether the project raises the version of a transitive dependency from a
    /// central pin alone, as <c>CentralPackageTransitivePinningEnabled</c> asks it to.
    /// </summary>
    /// <remarks>
    /// <para>Where this holds, a central pin binds a package the project never references, so
    /// <see cref="DirectReferences"/> no longer says which of the repository's pins are in use.</para>
    /// </remarks>
    public required bool PinsTransitively { get; init; }

    /// <summary>Gets what the restore logged about the project, in the order it logged it.</summary>
    public required IReadOnlyList<AssetsLogEntry> Logs { get; init; }
}
