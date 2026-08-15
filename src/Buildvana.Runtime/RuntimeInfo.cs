// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Run-time information about the <c>bv</c> run a hook belongs to: the running version, how the run was
/// launched, the absolute paths of the run's well-known directories and configuration file, and the
/// resolved configuration itself. Shared by every hook's args.
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

    /// <summary>
    /// Gets the absolute path of the configuration file this run read (<c>buildvana.json</c> or
    /// <c>buildvana.jsonc</c>), or <see langword="null"/> when the repository has none.
    /// </summary>
    /// <remarks>
    /// <para>This member names the source file, for a hook that works on the file itself — rewriting a value,
    /// checking it into the post-release commit — and must act on the very file <c>bv</c> read rather than
    /// guess which one it was. A hook that reads settings uses <see cref="Configuration"/> instead.</para>
    /// <para>Required of whoever writes the args, so that a run always states which file it read; the value is
    /// nonetheless <see langword="null"/> when the repository has no configuration file, which a repository
    /// whose home directory is marked by Git alone legitimately does not.</para>
    /// </remarks>
    public required string? ConfigFile { get; init; }

    /// <summary>
    /// Gets the resolved configuration of the run: every setting at its effective value, with the
    /// configuration file, the command line, and the built-in defaults already composed.
    /// </summary>
    /// <remarks>
    /// <para>This is a snapshot, taken when the args are written and embedded in every args file
    /// deliberately: args files can be re-run by hand after the fact, and a hook must see the settings of
    /// the run its args belong to, not whatever the configuration file happens to say by then.</para>
    /// </remarks>
    public required BuildvanaConfig Configuration { get; init; }
}
