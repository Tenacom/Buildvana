// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What <see cref="UpdatePolicyEngine"/> made of one pin: the outcome, the target when there is one, and the
/// latest versions the candidate set holds, whether or not the policy allows them.
/// </summary>
internal sealed record TargetSelection
{
    /// <summary>Gets the outcome of the selection.</summary>
    public required TargetSelectionOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the version the pin moves to, or <see langword="null"/> when it does not move. It is non-null
    /// exactly when <see cref="Outcome"/> is <see cref="TargetSelectionOutcome.Update"/>.
    /// </summary>
    public NuGetVersion? Target { get; init; }

    /// <summary>
    /// Gets the highest stable candidate, or <see langword="null"/> when there is none or nothing was
    /// resolved.
    /// </summary>
    public NuGetVersion? LatestStable { get; init; }

    /// <summary>
    /// Gets the highest prerelease candidate, or <see langword="null"/> when there is none or nothing was
    /// resolved.
    /// </summary>
    public NuGetVersion? LatestPreview { get; init; }
}
