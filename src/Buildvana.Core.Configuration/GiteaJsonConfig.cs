// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>hosts.gitea</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GiteaJsonConfig
{
    /// <summary>Gets the name of the environment variable holding the Gitea access token.</summary>
    [Description("Name of the environment variable that holds the Gitea access token.")]
    public string? TokenEnv { get; init; }
}
