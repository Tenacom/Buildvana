// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.IO.Gitignore;

// ReSharper disable once ClassCannotBeInstantiated // False positive: TryParse, in the parsing part, instantiates the class
partial class GitignorePattern
{
    /// <summary>
    /// Determines whether the pattern matches a path.
    /// </summary>
    /// <param name="pathComponents">The path to test, one component per element, relative to the
    /// directory the pattern applies from. An empty path matches nothing.</param>
    /// <param name="isDirectory"><see langword="true"/> when the path names a directory.</param>
    /// <param name="ignoreCase"><see langword="true"/> to ignore letter case.</param>
    /// <returns><see langword="true"/> when the pattern matches.</returns>
    /// <remarks>
    /// <para>The path must name the entry the question is about: matching a directory does not implicitly
    /// match the paths inside it. A traversal gets that effect by pruning matched directories — which is
    /// also what enforces gitignore(5)'s rule that no negation can re-include a file whose parent
    /// directory is excluded.</para>
    /// </remarks>
    public bool Matches(ReadOnlySpan<string> pathComponents, bool isDirectory, bool ignoreCase)
    {
        if (pathComponents.IsEmpty)
        {
            return false;
        }

        if (IsDirectoryOnly && !isDirectory)
        {
            return false;
        }

        return MatchSegments(_segments, pathComponents, ignoreCase);
    }

    // Greedy match with single-point backtracking: an any-depth segment first matches zero components,
    // and on a later mismatch the most recent any-depth segment consumes one more. This is the classic
    // iterative glob algorithm lifted one level up: segments play the role of pattern characters and
    // components the role of text characters; MatchTokens below is the same algorithm at the character
    // level, with AnyRun in the any-depth role.
    private static bool MatchSegments(
        ReadOnlySpan<GitignoreSegment> segments,
        ReadOnlySpan<string> components,
        bool ignoreCase)
    {
        var segmentIndex = 0;
        var componentIndex = 0;
        var backtrackSegmentIndex = -1;
        var backtrackComponentIndex = 0;
        while (componentIndex < components.Length)
        {
            if (segmentIndex < segments.Length && segments[segmentIndex].IsAnyDepth)
            {
                backtrackSegmentIndex = segmentIndex;
                backtrackComponentIndex = componentIndex;
                segmentIndex++;
                continue;
            }

            var matchesHere = segmentIndex < segments.Length
                && MatchTokens(segments[segmentIndex].Tokens, components[componentIndex], ignoreCase);
            if (matchesHere)
            {
                segmentIndex++;
                componentIndex++;
                continue;
            }

            if (backtrackSegmentIndex < 0)
            {
                return false;
            }

            backtrackComponentIndex++;
            segmentIndex = backtrackSegmentIndex + 1;
            componentIndex = backtrackComponentIndex;
        }

        while (segmentIndex < segments.Length && segments[segmentIndex].IsAnyDepth)
        {
            segmentIndex++;
        }

        return segmentIndex == segments.Length;
    }

    private static bool MatchTokens(
        ReadOnlySpan<GitignoreToken> tokens,
        ReadOnlySpan<char> component,
        bool ignoreCase)
    {
        var tokenIndex = 0;
        var charIndex = 0;
        var backtrackTokenIndex = -1;
        var backtrackCharIndex = 0;
        while (charIndex < component.Length)
        {
            if (tokenIndex < tokens.Length && tokens[tokenIndex].Kind == GitignoreTokenKind.AnyRun)
            {
                backtrackTokenIndex = tokenIndex;
                backtrackCharIndex = charIndex;
                tokenIndex++;
                continue;
            }

            var matchesHere = tokenIndex < tokens.Length
                && MatchesChar(tokens[tokenIndex], component[charIndex], ignoreCase);
            if (matchesHere)
            {
                tokenIndex++;
                charIndex++;
                continue;
            }

            if (backtrackTokenIndex < 0)
            {
                return false;
            }

            backtrackCharIndex++;
            tokenIndex = backtrackTokenIndex + 1;
            charIndex = backtrackCharIndex;
        }

        while (tokenIndex < tokens.Length && tokens[tokenIndex].Kind == GitignoreTokenKind.AnyRun)
        {
            tokenIndex++;
        }

        return tokenIndex == tokens.Length;
    }

    private static bool MatchesChar(GitignoreToken token, char c, bool ignoreCase)
    {
        return token.Kind switch
        {
            GitignoreTokenKind.Literal => CharsEqual(token.Value, c, ignoreCase),
            GitignoreTokenKind.AnyChar => true,
            GitignoreTokenKind.CharClass => token.CharClass!.Matches(c, ignoreCase),
            _ => false, // AnyRun is consumed by MatchTokens and never reaches here.
        };
    }

    // Invariant simple case folding — the folding OrdinalIgnoreCase comparisons use. Git folds with plain
    // ASCII tolower (wildmatch.c); invariant folding agrees on ASCII and extends the same idea to the
    // rest of the character set. Never the current culture: matching must not vary with the host locale.
    private static bool CharsEqual(char a, char b, bool ignoreCase)
    {
        if (a == b)
        {
            return true;
        }

        return ignoreCase && char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
    }
}
