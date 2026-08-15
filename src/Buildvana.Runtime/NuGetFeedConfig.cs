// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// A resolved NuGet push feed. The configuration schema requires both members whenever a feed is stated,
/// so a resolved feed always carries them.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetFeedConfig
{
    /// <summary>Gets the source URL of the NuGet feed.</summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the name of the environment variable holding the feed API key.
    /// The key itself is read on demand through the <c>GetApiKey</c> extension method, never stored.
    /// </summary>
    public required string ApiKeyEnv { get; init; }
}
