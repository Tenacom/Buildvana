// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>nuget</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetJsonConfig
{
    /// <summary>Gets the push feeds, one per channel.</summary>
    [Description("Push feeds, one per channel.")]
    public NuGetFeedsJsonConfig? Feeds { get; init; }
}
