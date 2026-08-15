// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved GitHub integration configuration.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitHubConfig
{
    /// <summary>
    /// Gets the name of the environment variable holding the GitHub access token.
    /// The token itself is read on demand through the <c>GetToken</c> extension method, never stored.
    /// </summary>
    public string TokenEnv { get; init; } = "GITHUB_TOKEN";
}
