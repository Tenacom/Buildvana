// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved configuration of the Forgejo host, from the <c>hosts.forgejo</c> section.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ForgejoConfig
{
    /// <summary>
    /// Gets the name of the environment variable holding the Forgejo access token.
    /// The token itself is read on demand through the <c>GetToken</c> extension method, never stored.
    /// </summary>
    public string TokenEnv { get; init; } = "FORGEJO_TOKEN";
}
