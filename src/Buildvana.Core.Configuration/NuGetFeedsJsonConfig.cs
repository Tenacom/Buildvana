// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>nuget.feeds</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetFeedsJsonConfig
{
    /// <summary>Gets the feed that prerelease versions are pushed to.</summary>
    [Description("Feed that prerelease versions are pushed to. When omitted, prerelease versions are pushed to the release feed.")]
    public NuGetFeedJsonConfig? Prerelease { get; init; }

    /// <summary>Gets the feed that stable versions are pushed to.</summary>
    [Description("Feed that stable versions are pushed to.")]
    public NuGetFeedJsonConfig? Release { get; init; }
}
