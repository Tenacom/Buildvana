// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO;

/// <summary>
/// <para>An ordered list of gitignore patterns, typically the contents of one gitignore file.</para>
/// <para>Like Git, the list answers for one source at a time: the last matching pattern decides, and
/// no pattern matching at all is a decision of its own, distinct from both others, so that a caller
/// can fall back to sources of lower precedence - for example, the gitignore files of parent
/// directories.</para>
/// </summary>
public sealed class GitIgnorePatternList
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitIgnorePatternList"/> class from parsed patterns.
    /// </summary>
    /// <param name="patterns">The patterns, in source order.</param>
    public GitIgnorePatternList(IEnumerable<GitIgnorePattern> patterns)
    {
        Guard.IsNotNull(patterns);
        Patterns = [.. patterns];
    }

    /// <summary>
    /// Gets the patterns, in source order.
    /// </summary>
    public IReadOnlyList<GitIgnorePattern> Patterns { get; }

    /// <summary>
    /// Parses the lines of a gitignore file, skipping blank lines and comments.
    /// </summary>
    /// <param name="lines">The lines, in source order.</param>
    /// <param name="matchCasing">How matching treats character casing;
    /// see <see cref="GitIgnorePattern.TryParse(string, MatchCasing, out GitIgnorePattern)"/>.</param>
    /// <returns>The parsed list.</returns>
    public static GitIgnorePatternList Parse(
        IEnumerable<string> lines,
        MatchCasing matchCasing = MatchCasing.CaseSensitive)
    {
        Guard.IsNotNull(lines);
        List<GitIgnorePattern> patterns = [];
        foreach (var line in lines)
        {
            if (GitIgnorePattern.TryParse(line, matchCasing, out var pattern))
            {
                patterns.Add(pattern);
            }
        }

        return new(patterns);
    }

    /// <summary>
    /// Gets this list's decision for a path.
    /// </summary>
    /// <param name="relativePath">The path to test, relative to the directory holding the list's
    /// source file: slash-separated, with no leading or trailing slash.</param>
    /// <param name="isDirectory">Whether <paramref name="relativePath"/> is a directory.</param>
    /// <returns>The decision of the last matching pattern, or <see cref="GitIgnoreDecision.None"/>
    /// when no pattern matches.</returns>
    /// <remarks>
    /// <para>The decision is for <paramref name="relativePath"/> itself. Per Git's rules a caller
    /// walking a tree never descends into an ignored directory, so a decision on a path whose parent
    /// is already ignored has no effect, <see cref="GitIgnoreDecision.Reinclude"/> included.</para>
    /// </remarks>
    public GitIgnoreDecision GetDecision(string relativePath, bool isDirectory)
    {
        Guard.IsNotNullOrEmpty(relativePath);
        for (var i = Patterns.Count - 1; i >= 0; i--)
        {
            var pattern = Patterns[i];
            if (pattern.IsMatch(relativePath, isDirectory))
            {
                return pattern.IsNegation ? GitIgnoreDecision.Reinclude : GitIgnoreDecision.Ignore;
            }
        }

        return GitIgnoreDecision.None;
    }
}
