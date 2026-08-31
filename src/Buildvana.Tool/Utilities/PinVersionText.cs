// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// States a target version in the place a file already gives to a version.
/// </summary>
/// <remarks>
/// <para>MSBuild carries the layout of a <c>&lt;Version&gt;</c> child element into the value it evaluates, so
/// the text a splice replaces may have whitespace around the version. That whitespace belongs to the file,
/// and a rewrite gives it back.</para>
/// </remarks>
internal static class PinVersionText
{
    /// <summary>
    /// States the target version in place of the version a file holds.
    /// </summary>
    /// <param name="versionText">The version text as the file states it, whitespace included.</param>
    /// <param name="target">The version to state.</param>
    /// <returns>The text to write in place of <paramref name="versionText"/>, or <see langword="null"/> when
    /// the text is not a literal version, or already states the target.</returns>
    public static string? Restate(string versionText, NuGetVersion target)
    {
        var start = 0;
        while (start < versionText.Length && char.IsWhiteSpace(versionText[start]))
        {
            start++;
        }

        var end = versionText.Length;
        while (end > start && char.IsWhiteSpace(versionText[end - 1]))
        {
            end--;
        }

        var core = versionText[start..end];
        if (!NuGetVersion.TryParse(core, out var current) || VersionComparer.VersionRelease.Equals(current, target))
        {
            return null;
        }

        return versionText[..start] + target.ToNormalizedString() + versionText[end..];
    }
}
