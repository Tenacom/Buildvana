// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The resolved Git author/committer identity used by automated commits. The configuration schema requires
/// both members whenever <c>git.identity</c> is stated, so a resolved identity always carries them.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record GitIdentityConfig
{
    /// <summary>Gets the display name of the Git identity.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the email address of the Git identity.</summary>
    public required string Email { get; init; }
}
