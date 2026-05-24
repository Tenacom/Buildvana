// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures GitHub integration.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitHubConfig
{
    /// <summary>Gets the name of the environment variable holding the GitHub access token.</summary>
    [Description("Name of the environment variable that holds the GitHub access token.")]
    public string? TokenEnv { get; init; }
}
