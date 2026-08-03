// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Describes the version being released by a <c>bv release</c> run.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ReleaseInfo
{
    /// <summary>
    /// Gets the version being released, in simple <c>MAJOR.MINOR.PATCH</c> form, without any prerelease tag.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the version being released, in full semantic version form: <see cref="Version"/>, plus
    /// <c>-tag</c> when the version is a prerelease. This is the form used by release tags and embedded
    /// in artifact names.
    /// </summary>
    public required string SemVer { get; init; }

    /// <summary>
    /// Gets the previously released version (the latest release tag reachable from <c>HEAD</c>),
    /// or <see langword="null"/> when no previous release exists.
    /// </summary>
    public required string? PreviousVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the version being released is a prerelease.
    /// </summary>
    public required bool IsPrerelease { get; init; }

    /// <summary>
    /// Gets a value indicating whether the release is a public release.
    /// </summary>
    public required bool IsPublicRelease { get; init; }
}
