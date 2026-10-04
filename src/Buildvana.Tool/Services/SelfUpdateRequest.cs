// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// What an invocation of <c>bv self-update</c> asks of the update: which version the repository moves to, and
/// whether a downgrade is allowed.
/// </summary>
/// <remarks>
/// <para><see cref="To"/>, <see cref="Preview"/>, and <see cref="Repair"/> each name the target version, so at
/// most one of them is set. The command line rejects a combination before a request is built. With none of
/// them set, <see cref="SelfUpdateTargetResolver"/> picks the target from what the package sources list.</para>
/// </remarks>
internal sealed record SelfUpdateRequest
{
    /// <summary>Gets the request of a run that asks for nothing beyond the default target.</summary>
    public static SelfUpdateRequest Default { get; } = new();

    /// <summary>Gets the version the invocation states, or <see langword="null"/> when it states none.</summary>
    public NuGetVersion? To { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target is the latest version the package sources list, prereleases
    /// included.
    /// </summary>
    public bool Preview { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target is the version the tool manifest pins, with no package source
    /// asked.
    /// </summary>
    public bool Repair { get; init; }

    /// <summary>
    /// Gets a value indicating whether the update may downgrade pins that are newer than the target version.
    /// </summary>
    public bool Force { get; init; }
}
