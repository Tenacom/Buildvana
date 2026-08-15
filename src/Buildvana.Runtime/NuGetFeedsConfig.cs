// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved NuGet push feeds, one per channel.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetFeedsConfig
{
    /// <summary>
    /// Gets the feed that prerelease versions are pushed to, or <see langword="null"/> when no feed is
    /// configured for prereleases at all: a configuration that states only a release feed resolves this
    /// to that feed, the fallback being applied when the configuration is composed rather than left to
    /// consumers.
    /// </summary>
    public NuGetFeedConfig? Prerelease { get; init; }

    /// <summary>
    /// Gets the feed that stable versions are pushed to, or <see langword="null"/> when no feed is
    /// configured for them.
    /// </summary>
    public NuGetFeedConfig? Release { get; init; }
}
