// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What a run made of one pin.
/// </summary>
/// <remarks>
/// <para>A check run and an apply run reach the same state for the same pin. The two differ in what happens
/// next, not in what was decided.</para>
/// </remarks>
internal enum PinResolutionState
{
    /// <summary>The pin is at the version its policy would take it to.</summary>
    UpToDate,

    /// <summary>The pin moved to its target, or would in a check run.</summary>
    Updated,

    /// <summary>The effective policy is <c>disable</c>, so nothing was resolved.</summary>
    Disabled,

    /// <summary>The pin is not a literal exact version, so nothing was resolved.</summary>
    Unmanaged,

    /// <summary>The invocation did not resolve the pin.</summary>
    Skipped,

    /// <summary>
    /// Resolution found no version the policy allows: a prerelease pin under a policy that takes only stable
    /// versions with no stable release at or above it, a delisted pin with nothing listed above it, or a
    /// short-term support .NET SDK pin under the <c>lts</c> policy.
    /// </summary>
    Held,
}
