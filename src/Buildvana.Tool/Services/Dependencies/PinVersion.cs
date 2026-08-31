// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the version a pin states, the way NuGet reads it.
/// </summary>
/// <remarks>
/// <para>The judgment is NuGet's own, through <see cref="NuGetVersion"/> and <see cref="VersionRange"/>:
/// what NuGet accepts as one exact version is what an automatic update may move, and everything else is a
/// decision the pin's author made and <c>bv</c> leaves alone.</para>
/// </remarks>
internal static class PinVersion
{
    /// <summary>
    /// Reads the version text of a pin.
    /// </summary>
    /// <param name="text">The version text, as the pin states it.</param>
    /// <param name="version">When this method returns, the version, if the text states exactly one;
    /// otherwise, <see langword="null"/>.</param>
    /// <returns>The form of the text.</returns>
    public static PinVersionForm Read(string? text, out NuGetVersion? version)
    {
        version = null;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return PinVersionForm.Unrecognized;
        }

        // An exact version parses as a range too, as its own minimum, so it is tried first.
        if (NuGetVersion.TryParse(trimmed, out var parsed))
        {
            version = parsed;
            return PinVersionForm.Literal;
        }

        if (!VersionRange.TryParse(trimmed, out var range))
        {
            return PinVersionForm.Unrecognized;
        }

        return range.IsFloating ? PinVersionForm.Floating
            : IsBracketExact(range) ? PinVersionForm.BracketExact
            : PinVersionForm.Range;
    }

    // `[13.0.4]` states one version by naming it as both ends of a closed range.
    private static bool IsBracketExact(VersionRange range)
        => range is { HasLowerAndUpperBounds: true, IsMinInclusive: true, IsMaxInclusive: true }
            && VersionComparer.VersionRelease.Equals(range.MinVersion, range.MaxVersion);
}
