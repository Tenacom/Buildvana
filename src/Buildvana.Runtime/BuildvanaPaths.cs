// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The absolute paths of the well-known directories of a <c>bv</c> run.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record BuildvanaPaths
{
    /// <summary>
    /// Gets the absolute path of the home directory. This is also the working directory of hooks.
    /// </summary>
    public required string HomeDirectory { get; init; }

    /// <summary>
    /// Gets the absolute path of the directory containing the build artifacts.
    /// </summary>
    public required string ArtifactsDirectory { get; init; }

    /// <summary>
    /// Gets the absolute path of bv's scratch directory (<see cref="WellKnownPaths.ScratchDirectory"/>),
    /// where hooks can write temporary files without affecting working-tree change detection.
    /// </summary>
    public required string ScratchDirectory { get; init; }
}
