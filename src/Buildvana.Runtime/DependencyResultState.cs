// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// What a run of <c>bv dependencies</c> made of one pin.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public enum DependencyResultState
{
    /// <summary>The pin is at the version its policy would take it to.</summary>
    UpToDate,

    /// <summary>The pin was moved to its target, or, in a check run, would be.</summary>
    Updated,

    /// <summary>The effective policy is <c>disable</c>, and nothing was resolved.</summary>
    Disabled,

    /// <summary>The pin is not a literal exact version, and nothing was resolved.</summary>
    Unmanaged,

    /// <summary>The invocation did not resolve the pin: an argument left it out, or the subcommand resolves nothing.</summary>
    Skipped,

    /// <summary>Resolution found no version the policy allows.</summary>
    Held,
}
