// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Configures Git-related behavior.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitConfig
{
    /// <summary>Gets the Git identity used by automated commits.</summary>
    [Description("Git identity used by automated commits.")]
    public GitIdentityConfig? Identity { get; init; }
}
