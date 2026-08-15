// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// A resolved NuGet push feed.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetFeedConfig
{
    /// <summary>Gets the source URL of the NuGet feed.</summary>
    public string? Source { get; init; }

    /// <summary>Gets the name of the environment variable holding the feed API key.</summary>
    public string? ApiKeyEnv { get; init; }
}
