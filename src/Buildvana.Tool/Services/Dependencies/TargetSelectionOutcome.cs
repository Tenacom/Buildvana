// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What an update policy answered for one pin.
/// </summary>
internal enum TargetSelectionOutcome
{
    /// <summary>
    /// The policy is <c>disable</c>, so no target was resolved. The latest-version members of
    /// <see cref="TargetSelection"/> still report whatever candidates the caller supplied.
    /// </summary>
    Disabled,

    /// <summary>The pin already sits at the best version its policy allows.</summary>
    UpToDate,

    /// <summary>The policy allows a version above the pin: <see cref="TargetSelection.Target"/> names it.</summary>
    Update,

    /// <summary>The policy allows no version at or above the pin, so the pin does not move.</summary>
    Held,
}
