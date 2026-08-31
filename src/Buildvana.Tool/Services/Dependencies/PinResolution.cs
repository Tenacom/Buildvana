// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What a run made of one pin of an id-shaped scope: the state it reached, the version it moves to when it
/// moves, and what the sources have beyond the policy.
/// </summary>
/// <remarks>
/// <para>A pin whose resolution failed is stated as <see cref="PinResolutionState.Skipped"/> and never
/// reaches a report: the run that produced it stops instead, naming every pin it could not resolve.</para>
/// </remarks>
internal sealed record PinResolution
{
    /// <summary>Gets the pin, as the repository states it.</summary>
    public required DependencyPin Pin { get; init; }

    /// <summary>Gets the policy governing the pin.</summary>
    public required PackageUpdatePolicy Policy { get; init; }

    /// <summary>Gets what the run made of the pin.</summary>
    public required PinResolutionState State { get; init; }

    /// <summary>
    /// Gets the version the pin moves to, or <see langword="null"/> when it does not move. It is non-null
    /// exactly when <see cref="State"/> is <see cref="PinResolutionState.Updated"/>.
    /// </summary>
    public NuGetVersion? Target { get; init; }

    /// <summary>
    /// Gets the highest stable version the sources list, or <see langword="null"/> when nothing was
    /// resolved. It may lie beyond the policy, which is what makes it worth reporting.
    /// </summary>
    public NuGetVersion? LatestStable { get; init; }

    /// <summary>
    /// Gets the highest prerelease version the sources list, or <see langword="null"/> when nothing was
    /// resolved.
    /// </summary>
    public NuGetVersion? LatestPreview { get; init; }

    /// <summary>Gets what a reader must know about the pin, or an empty string when there is nothing.</summary>
    public string Note { get; init; } = string.Empty;
}
