// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved Git configuration.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitConfig
{
    /// <summary>
    /// Gets the Git identity used by automated commits, or <see langword="null"/> when no identity is
    /// configured.
    /// </summary>
    public GitIdentityConfig? Identity { get; init; }
}
