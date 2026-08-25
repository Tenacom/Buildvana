// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// One parsed pattern line from a gitignore file.
/// </summary>
/// <remarks>
/// <para>The semantics implemented here are those of
/// <see href="https://git-scm.com/docs/gitignore">gitignore(5)</see> ("PATTERN FORMAT") and of Git's
/// matcher, <see href="https://github.com/git/git/blob/master/wildmatch.c"><c>wildmatch.c</c></see>.
/// A pattern is parsed once into slash-delimited segments of literal, wildcard, and
/// bracket-expression tokens; matching interprets those tokens directly, so that a pattern's behavior can
/// be stepped through in a debugger rather than reverse-engineered from a generated regular expression.</para>
/// <para>Parsing normalizes two shapes. A pattern with no directory separator other than a trailing one is
/// unanchored, matching at any depth; it gains a leading any-depth segment, making every match a full-path
/// match. A trailing <c>**</c> becomes an any-depth segment followed by a match-one-component segment,
/// encoding gitignore(5)'s "everything inside", which requires at least one component below the pattern;
/// the any-depth segment itself then uniformly matches zero or more components everywhere.</para>
/// </remarks>
public sealed partial class GitignorePattern
{
    private readonly GitignoreSegment[] _segments;

    private GitignorePattern(string text, bool isNegated, bool isDirectoryOnly, GitignoreSegment[] segments)
    {
        Text = text;
        IsNegated = isNegated;
        IsDirectoryOnly = isDirectoryOnly;
        _segments = segments;
    }

    /// <summary>
    /// Gets the original line this pattern was parsed from.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets a value indicating whether the pattern is a negation (leading <c>!</c>), re-including
    /// paths that earlier patterns excluded.
    /// </summary>
    public bool IsNegated { get; }

    /// <summary>
    /// Gets a value indicating whether the pattern matches directories only (trailing <c>/</c>).
    /// </summary>
    public bool IsDirectoryOnly { get; }

    /// <summary>
    /// Gets the pattern's segments, normalized as described in the class remarks.
    /// </summary>
    public IReadOnlyList<GitignoreSegment> Segments => _segments;

    /// <inheritdoc/>
    public override string ToString() => Text;
}
