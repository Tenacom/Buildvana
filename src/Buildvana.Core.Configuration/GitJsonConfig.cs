// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;

namespace Buildvana.Core.Configuration;

/// <summary>
/// Models the <c>git</c> section of a Buildvana configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitJsonConfig
{
    /// <summary>Gets the Git identity used by automated commits.</summary>
    [Description("Git identity used by automated commits.")]
    public GitIdentityJsonConfig? Identity { get; init; }
}
