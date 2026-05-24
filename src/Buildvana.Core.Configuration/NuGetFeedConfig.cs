// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures a single NuGet push feed.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NuGetFeedConfig
{
    /// <summary>Gets the source URL of the NuGet feed.</summary>
    [Description("Source URL of the NuGet feed.")]
    public string? Source { get; init; }

    /// <summary>Gets the name of the environment variable holding the feed API key.</summary>
    [Description("Name of the environment variable that holds the feed API key.")]
    public string? ApiKeyEnv { get; init; }
}
