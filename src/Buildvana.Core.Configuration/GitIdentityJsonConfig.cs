// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>git.identity</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitIdentityJsonConfig
{
    /// <summary>Gets the display name of the Git identity.</summary>
    [Description("Display name used as the Git author/committer.")]
    public string? Name { get; init; }

    /// <summary>Gets the email address of the Git identity.</summary>
    [Description("Email address used as the Git author/committer.")]
    public string? Email { get; init; }
}
