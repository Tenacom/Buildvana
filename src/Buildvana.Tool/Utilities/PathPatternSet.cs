// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Buildvana.Core.IO;
using Buildvana.Core.IO.Gitignore;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// A set of repository files, stated as gitignore-syntax patterns and tested against paths relative to the
/// home directory.
/// </summary>
/// <remarks>
/// <para>The syntax is the one a repository already knows, down to the last-match-wins rule and the leading
/// <c>!</c> that takes a file back out of the set. A pattern that names a directory takes its whole subtree
/// with it.</para>
/// </remarks>
internal sealed class PathPatternSet
{
    private readonly GitignoreFile _patterns;
    private readonly bool _ignoresCase;

    private PathPatternSet(GitignoreFile patterns, bool ignoresCase)
    {
        _patterns = patterns;
        _ignoresCase = ignoresCase;
    }

    /// <summary>
    /// Reads a set from its patterns.
    /// </summary>
    /// <param name="patterns">The gitignore-syntax patterns, in the order they are stated: the last one that
    /// matches a path decides.</param>
    /// <returns>The set.</returns>
    public static PathPatternSet Parse(IReadOnlyList<string> patterns)
    {
        Guard.IsNotNull(patterns);
        return new PathPatternSet(GitignoreFile.Parse(patterns), CaseSensitivityMode.SystemDefault.IgnoresCase());
    }

    /// <summary>
    /// Determines whether a file belongs to the set.
    /// </summary>
    /// <param name="relativePath">The path of the file, relative to the home directory, with forward slashes.</param>
    /// <returns><see langword="true"/> if the file belongs to the set; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string relativePath)
    {
        Guard.IsNotNull(relativePath);

        // Mirror of the gitignore walk with "select" in place of "ignore": a matched directory selects its
        // whole subtree, and a file needs a pattern of its own only when no ancestor directory matched.
        var components = relativePath.Split('/');
        for (var count = 1; count <= components.Length; count++)
        {
            var isDirectory = count < components.Length;
            if (_patterns.Evaluate(components.AsSpan(0, count), isDirectory, _ignoresCase) == GitignoreDecision.Ignore)
            {
                return true;
            }
        }

        return false;
    }
}
