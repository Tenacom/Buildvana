// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Run-time information about the <c>bv</c> run a hook belongs to: the running version, how the run was
/// launched, and the absolute paths of the run's well-known directories. Shared by every hook's args.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record RuntimeInfo
{
    /// <summary>
    /// Gets the version of the running bv, in semantic version form without build metadata.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the version of the bv that delegated the run to the version pinned in the repository's tool
    /// manifest, or <see langword="null"/> when the run was not delegated.
    /// </summary>
    public string? DelegatingVersion { get; init; }

    /// <summary>
    /// Gets the absolute path of the home directory, without a trailing directory separator.
    /// This is also the working directory of hooks.
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
