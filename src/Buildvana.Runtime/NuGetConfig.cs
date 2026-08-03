// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Configures NuGet package publishing.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetConfig
{
    /// <summary>Gets the push feeds, one per channel.</summary>
    [Description("Push feeds, one per channel.")]
    public NuGetFeedsConfig? Feeds { get; init; }
}
