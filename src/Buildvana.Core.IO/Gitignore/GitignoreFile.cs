// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// An ordered list of gitignore patterns, typically the parsed contents of one <c>.gitignore</c> file.
/// </summary>
public sealed class GitignoreFile
{
    private readonly GitignorePattern[] _patterns;

    private GitignoreFile(GitignorePattern[] patterns)
    {
        _patterns = patterns;
    }

    /// <summary>
    /// Gets the patterns, in file order. Lines that decide nothing — blank lines, comments, patterns
    /// that can never match — are not represented.
    /// </summary>
    public IReadOnlyList<GitignorePattern> Patterns => _patterns;

    /// <summary>
    /// Parses gitignore lines.
    /// </summary>
    /// <param name="lines">The lines, without line terminators.</param>
    /// <returns>The parsed pattern list.</returns>
    public static GitignoreFile Parse(IReadOnlyList<string> lines)
    {
        Guard.IsNotNull(lines);
        var patterns = new List<GitignorePattern>();
        foreach (var line in lines)
        {
            if (GitignorePattern.TryParse(line) is { } pattern)
            {
                patterns.Add(pattern);
            }
        }

        return new([.. patterns]);
    }

    /// <summary>
    /// Evaluates a path against the pattern list.
    /// </summary>
    /// <param name="pathComponents">The path to test, one component per element, relative to the
    /// directory this pattern list applies from.</param>
    /// <param name="isDirectory"><see langword="true"/> when the path names a directory.</param>
    /// <param name="ignoreCase"><see langword="true"/> to ignore letter case.</param>
    /// <returns>The decision of the last matching pattern, or <see cref="GitignoreDecision.None"/>
    /// when no pattern matches.</returns>
    public GitignoreDecision Evaluate(ReadOnlySpan<string> pathComponents, bool isDirectory, bool ignoreCase)
    {
        var decision = GitignoreDecision.None;
        foreach (var pattern in _patterns)
        {
            if (pattern.Matches(pathComponents, isDirectory, ignoreCase))
            {
                decision = pattern.IsNegated ? GitignoreDecision.Include : GitignoreDecision.Ignore;
            }
        }

        return decision;
    }
}
