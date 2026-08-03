// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Configures the NuGet push feeds, one per channel.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetFeedsConfig
{
    /// <summary>Gets the feed that prerelease versions are pushed to.</summary>
    [Description("Feed that prerelease versions are pushed to. When omitted, prerelease versions are pushed to the release feed.")]
    public NuGetFeedConfig? Prerelease { get; init; }

    /// <summary>Gets the feed that stable versions are pushed to.</summary>
    [Description("Feed that stable versions are pushed to.")]
    public NuGetFeedConfig? Release { get; init; }
}
