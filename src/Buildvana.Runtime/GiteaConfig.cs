// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved configuration of the Gitea host, from the <c>hosts.gitea</c> section.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GiteaConfig
{
    /// <summary>
    /// Gets the name of the environment variable holding the Gitea access token.
    /// The token itself is read on demand through the <c>GetToken</c> extension method, never stored.
    /// </summary>
    public string TokenEnv { get; init; } = "GITEA_TOKEN";
}
