// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// One slash-delimited element of a <see cref="GitignorePattern"/>: either the any-depth marker
/// (a segment consisting solely of <c>**</c>) or a token sequence matched against a single path component.
/// </summary>
public sealed class GitignoreSegment
{
    private readonly GitignoreToken[] _tokens;

    private GitignoreSegment(bool isAnyDepth, GitignoreToken[] tokens)
    {
        IsAnyDepth = isAnyDepth;
        _tokens = tokens;
    }

    /// <summary>
    /// Gets the segment that matches any number of path components, including none (<c>**</c>).
    /// </summary>
    public static GitignoreSegment AnyDepth { get; } = new(isAnyDepth: true, []);

    /// <summary>
    /// Gets a value indicating whether this segment is the any-depth marker (<c>**</c>).
    /// </summary>
    public bool IsAnyDepth { get; }

    /// <summary>
    /// Gets the tokens matched against a single path component. Empty for the any-depth marker.
    /// </summary>
    public ReadOnlySpan<GitignoreToken> Tokens => _tokens;

    /// <summary>
    /// Gets the segment holding a single <see cref="GitignoreToken.AnyRun"/> token, matching any one
    /// path component. Parsing rewrites a trailing <c>**</c> as <see cref="AnyDepth"/> followed by
    /// this segment.
    /// </summary>
    internal static GitignoreSegment AnyComponent { get; } = new(isAnyDepth: false, [GitignoreToken.AnyRun]);

    /// <summary>
    /// Creates a segment from a token sequence.
    /// </summary>
    /// <param name="tokens">The tokens matched against a single path component.</param>
    /// <returns>The segment.</returns>
    internal static GitignoreSegment Create(GitignoreToken[] tokens) => new(isAnyDepth: false, tokens);
}
