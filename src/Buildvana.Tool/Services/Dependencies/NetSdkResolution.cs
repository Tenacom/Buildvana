// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Configuration;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What a run made of the .NET SDK baseline.
/// </summary>
/// <remarks>
/// <para>The scope has a pin of its own shape and a policy of its own vocabulary, and one thing no other
/// scope has: a setting that must agree with the policy, which an apply run writes.</para>
/// </remarks>
internal sealed record NetSdkResolution
{
    /// <summary>Gets the baseline, as <c>global.json</c> states it.</summary>
    public required NetSdkPin Pin { get; init; }

    /// <summary>Gets the policy governing the scope.</summary>
    public required NetSdkUpdatePolicy Policy { get; init; }

    /// <summary>Gets what the run made of the baseline.</summary>
    public required PinResolutionState State { get; init; }

    /// <summary>
    /// Gets whether an apply run must write <c>sdk.allowPrerelease</c> to make it agree with the policy.
    /// A check run counts this as pending work of its own, whether or not the version moves.
    /// </summary>
    public required bool WritesAllowPrerelease { get; init; }

    /// <summary>
    /// Gets the version the baseline moves to, or <see langword="null"/> when it does not move.
    /// </summary>
    public NuGetVersion? Target { get; init; }

    /// <summary>Gets the highest stable release, or <see langword="null"/> when nothing was resolved.</summary>
    public NuGetVersion? LatestStable { get; init; }

    /// <summary>Gets the highest prerelease, or <see langword="null"/> when nothing was resolved.</summary>
    public NuGetVersion? LatestPreview { get; init; }

    /// <summary>Gets what a reader must know about the baseline, or an empty string when there is nothing.</summary>
    public string Note { get; init; } = string.Empty;
}
