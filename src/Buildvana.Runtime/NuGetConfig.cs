// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved NuGet package-publishing configuration.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetConfig
{
    /// <summary>Gets the resolved push feeds, one per channel.</summary>
    public NuGetFeedsConfig Feeds { get; init; } = new();
}
