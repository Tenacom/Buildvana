// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Tool.Utilities;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What every writer of pins needs: which pins move, and which version each of them moves to.
/// </summary>
/// <remarks>
/// <para>A pin is looked up by item type, id and version text. Ids and item types are compared without
/// regard to case, as MSBuild and NuGet compare them, and a version text is compared without the whitespace
/// a file may have put around it.</para>
/// </remarks>
internal static class PinWriting
{
    /// <summary>
    /// Names the pins a run moves.
    /// </summary>
    /// <param name="pins">What the run made of a scope's pins.</param>
    /// <returns>The pins that move.</returns>
    public static IEnumerable<PinResolution> Moving(IEnumerable<PinResolution> pins)
        => pins.Where(static pin => pin.State == PinResolutionState.Updated && pin.Target is not null);

    /// <summary>
    /// Indexes the versions a set of pins moves to, by what identifies each pin in its file.
    /// </summary>
    /// <param name="pins">The pins that move.</param>
    /// <returns>The lookup a rewrite consults for every declaration it walks past.</returns>
    public static Dictionary<PinKey, NuGetVersion> TargetsOf(IEnumerable<PinResolution> pins)
    {
        var targets = new Dictionary<PinKey, NuGetVersion>();
        foreach (var pin in pins)
        {
            targets[KeyOf(pin.Pin.ItemType, pin.Pin.Id, pin.Pin.VersionText)] = pin.Target!;
        }

        return targets;
    }

    /// <summary>
    /// States the version a declaration moves to, in the place the file gives it.
    /// </summary>
    /// <param name="targets">The lookup from <see cref="TargetsOf"/>.</param>
    /// <param name="itemType">The item type of the declaration, or <see langword="null"/> for a directive.</param>
    /// <param name="id">The id the declaration names.</param>
    /// <param name="versionText">The version text the declaration holds.</param>
    /// <returns>The text to write, or <see langword="null"/> to leave the declaration alone.</returns>
    public static string? Restate(Dictionary<PinKey, NuGetVersion> targets, string? itemType, string id, string versionText)
        => targets.TryGetValue(KeyOf(itemType, id, versionText), out var target)
            ? PinVersionText.Restate(versionText, target)
            : null;

    private static PinKey KeyOf(string? itemType, string id, string versionText)
        => new(
            (itemType ?? string.Empty).ToUpperInvariant(),
            id.ToUpperInvariant(),
            versionText.Trim());
}
