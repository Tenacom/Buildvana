// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Matches a package id against the patterns of the <c>dependencies.policies</c> configuration table.
/// </summary>
/// <remarks>
/// <para>The syntax is one wildcard and nothing else: <c>*</c> stands for any run of characters, including
/// none, and every other character stands for itself. The whole id must match, and matching ignores case,
/// package ids being case-insensitive. An id with no wildcard in it is therefore a pattern matching that id
/// alone.</para>
/// </remarks>
internal static class PackageIdPattern
{
    /// <summary>
    /// Determines whether a package id matches a pattern.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="id">The package id.</param>
    /// <returns><see langword="true"/> if the whole id matches; otherwise, <see langword="false"/>.</returns>
    public static bool Matches(string pattern, string id)
    {
        Guard.IsNotNull(pattern);
        Guard.IsNotNull(id);

        // The classic two-index walk with one backtracking point: on a mismatch after a wildcard, the
        // wildcard eats one more character of the id and matching resumes from there. Recursion is the other
        // way to write this, and a pattern of many wildcards would nest it as deep as the id is long.
        var patternIndex = 0;
        var idIndex = 0;
        var starIndex = -1;
        var idIndexAtStar = 0;
        while (idIndex < id.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex;
                idIndexAtStar = idIndex;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && IsSameCharacter(pattern[patternIndex], id[idIndex]))
            {
                patternIndex++;
                idIndex++;
            }
            else if (starIndex < 0)
            {
                return false;
            }
            else
            {
                patternIndex = starIndex + 1;
                idIndexAtStar++;
                idIndex = idIndexAtStar;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    // Folded both ways and invariantly: a pattern must match the same ids on every machine, whatever the
    // culture the run happens to have.
    private static bool IsSameCharacter(char a, char b)
        => a == b || char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
}
