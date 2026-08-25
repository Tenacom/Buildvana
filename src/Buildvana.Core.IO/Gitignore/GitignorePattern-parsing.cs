// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO.Gitignore;

partial class GitignorePattern
{
    /// <summary>
    /// Parses one gitignore line.
    /// </summary>
    /// <param name="line">The line to parse, without its line terminator.</param>
    /// <returns>The parsed pattern, or <see langword="null"/> when the line decides nothing:
    /// a blank line, a comment, or a pattern that can never match.</returns>
    /// <remarks>
    /// <para>Git treats three malformed shapes as patterns that never match rather than as errors: a
    /// trailing unescaped backslash (gitignore(5)), an unclosed bracket expression, and an unknown POSIX
    /// class name (both <c>wildmatch.c</c>). A pattern that never matches never decides anything, so this
    /// method folds all three into the <see langword="null"/> result — as it does a pattern with an empty
    /// segment (<c>a//b</c>), which no real path can have.</para>
    /// </remarks>
    public static GitignorePattern? TryParse(string line)
    {
        Guard.IsNotNull(line);
        if (line.Length == 0 || line[0] == '#')
        {
            return null;
        }

        var text = TrimTrailingSpaces(line);
        var isNegated = false;
        if (text.StartsWith('!'))
        {
            isNegated = true;
            text = text[1..];
        }

        var isDirectoryOnly = false;
        if (text.EndsWith('/'))
        {
            isDirectoryOnly = true;
            text = text[..^1];
        }

        // gitignore(5): a separator at the beginning or middle of the pattern anchors it to the directory
        // of the gitignore file itself; the trailing separator stripped above does not count.
        var isAnchored = false;
        if (text.StartsWith('/'))
        {
            isAnchored = true;
            text = text[1..];
        }

        if (text.Length == 0)
        {
            return null;
        }

        if (text.Contains('/', StringComparison.Ordinal))
        {
            isAnchored = true;
        }

        var parts = text.Split('/');
        var segments = new List<GitignoreSegment>(parts.Length + 2);
        if (!isAnchored)
        {
            // gitignore(5): "**/foo" matches the same as "foo". Prepending the any-depth segment makes
            // unanchored patterns full-path matches like all the others.
            segments.Add(GitignoreSegment.AnyDepth);
        }

        foreach (var part in parts)
        {
            if (!TryParseSegment(part, out var segment))
            {
                return null;
            }

            segments.Add(segment);
        }

        // gitignore(5): a trailing "/**" matches everything inside, which requires at least one component
        // below the pattern; rewriting it as "any depth, then any one component" keeps the any-depth
        // segment uniformly zero-or-more everywhere else.
        if (segments[^1].IsAnyDepth)
        {
            segments.Add(GitignoreSegment.AnyComponent);
        }

        return new GitignorePattern(line, isNegated, isDirectoryOnly, [.. segments]);
    }

    // gitignore(5): trailing spaces are ignored unless quoted with backslash. The backslash stays in
    // place; the tokenizer resolves all escapes.
    private static string TrimTrailingSpaces(string line)
    {
        var end = 0;
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == '\\' && i + 1 < line.Length)
            {
                i += 2;
                end = i;
                continue;
            }

            i++;
            if (line[i - 1] != ' ')
            {
                end = i;
            }
        }

        return line[..end];
    }

    private static bool TryParseSegment(string part, out GitignoreSegment segment)
    {
        segment = GitignoreSegment.AnyDepth;
        if (part.Length == 0)
        {
            return false;
        }

        if (part == "**")
        {
            return true;
        }

        var tokens = new List<GitignoreToken>();
        var i = 0;
        while (i < part.Length)
        {
            var c = part[i];
            switch (c)
            {
                case '\\':
                    if (i + 1 >= part.Length)
                    {
                        // gitignore(5): a backslash at the end of a pattern is an invalid pattern
                        // that never matches.
                        return false;
                    }

                    tokens.Add(GitignoreToken.Literal(part[i + 1]));
                    i += 2;
                    break;
                case '*':
                    // gitignore(5): consecutive asterisks not forming a whole "**" segment are regular
                    // asterisks; adjacent regular asterisks are equivalent to one.
                    tokens.Add(GitignoreToken.AnyRun);
                    while (i < part.Length && part[i] == '*')
                    {
                        i++;
                    }

                    break;
                case '?':
                    tokens.Add(GitignoreToken.AnyChar);
                    i++;
                    break;
                case '[':
                    if (!TryParseCharClass(part, ref i, tokens))
                    {
                        return false;
                    }

                    break;
                default:
                    tokens.Add(GitignoreToken.Literal(c));
                    i++;
                    break;
            }
        }

        segment = GitignoreSegment.Create([.. tokens]);
        return true;
    }

    // Mirrors the "[" arm of wildmatch.c's dowild: "!" or "^" negates; a "]" right after the opener (or
    // the negation marker) is a literal member; "-" forms a range only between members; a member, a range
    // endpoint, or the whole expression can be backslash-escaped; "[:name:]" adds a POSIX named class.
    private static bool TryParseCharClass(string part, ref int i, List<GitignoreToken> tokens)
    {
        var j = i + 1;
        var isNegated = false;
        if (j < part.Length && part[j] is '!' or '^')
        {
            isNegated = true;
            j++;
        }

        var chars = new StringBuilder();
        var ranges = new List<(char First, char Last)>();
        var namedClasses = new List<GitignoreNamedClass>();
        var hasPrev = false;
        var prev = '\0';
        var isFirstMember = true;
        while (true)
        {
            if (j >= part.Length)
            {
                // wildmatch.c: an unclosed bracket expression makes the whole pattern never match.
                return false;
            }

            var c = part[j];
            if (c == ']' && !isFirstMember)
            {
                break;
            }

            isFirstMember = false;
            if (c == '\\')
            {
                if (j + 1 >= part.Length)
                {
                    return false;
                }

                prev = part[j + 1];
                hasPrev = true;
                _ = chars.Append(prev);
                j += 2;
                continue;
            }

            var isRange = c == '-' && hasPrev && j + 1 < part.Length && part[j + 1] != ']';
            if (isRange)
            {
                j++;
                var last = part[j];
                if (last == '\\')
                {
                    if (j + 1 >= part.Length)
                    {
                        return false;
                    }

                    j++;
                    last = part[j];
                }

                // The range's first character stays in the literal members too, as in wildmatch.c: it
                // matched as a literal on the iteration before the "-" was seen. This only shows with a
                // descending range like "[c-a]", where the range matches nothing but "c" still does.
                ranges.Add((prev, last));
                hasPrev = false;
                j++;
                continue;
            }

            if (c == '[' && j + 1 < part.Length && part[j + 1] == ':')
            {
                if (!TryParseNamedClass(part, ref j, chars, namedClasses, ref prev, ref hasPrev))
                {
                    return false;
                }

                continue;
            }

            _ = chars.Append(c);
            prev = c;
            hasPrev = true;
            j++;
        }

        var charClass = new GitignoreCharClass(isNegated, chars.ToString(), [.. ranges], [.. namedClasses]);
        tokens.Add(GitignoreToken.ForCharClass(charClass));
        i = j + 1;
        return true;
    }

    // Handles "[:" inside a bracket expression, with j at the inner "[". On a well-formed "[:name:]",
    // adds the named class and advances j past it; wildmatch.c aborts the whole pattern when the name is
    // unknown, and falls back to reading the "[" as an ordinary member when no ":]" closes the class.
    private static bool TryParseNamedClass(
        string part,
        ref int j,
        StringBuilder chars,
        List<GitignoreNamedClass> namedClasses,
        ref char prev,
        ref bool hasPrev)
    {
        var close = part.IndexOf(']', j + 2);
        if (close < 0)
        {
            return false;
        }

        if (close - 1 <= j + 1 || part[close - 1] != ':')
        {
            _ = chars.Append('[');
            prev = '[';
            hasPrev = true;
            j++;
            return true;
        }

        if (GetNamedClass(part[(j + 2)..(close - 1)]) is not { } namedClass)
        {
            return false;
        }

        namedClasses.Add(namedClass);
        hasPrev = false;
        j = close + 1;
        return true;
    }

    private static GitignoreNamedClass? GetNamedClass(string name)
    {
        return name switch
        {
            "alnum" => GitignoreNamedClass.Alnum,
            "alpha" => GitignoreNamedClass.Alpha,
            "blank" => GitignoreNamedClass.Blank,
            "cntrl" => GitignoreNamedClass.Cntrl,
            "digit" => GitignoreNamedClass.Digit,
            "graph" => GitignoreNamedClass.Graph,
            "lower" => GitignoreNamedClass.Lower,
            "print" => GitignoreNamedClass.Print,
            "punct" => GitignoreNamedClass.Punct,
            "space" => GitignoreNamedClass.Space,
            "upper" => GitignoreNamedClass.Upper,
            "xdigit" => GitignoreNamedClass.Xdigit,
            _ => null,
        };
    }
}
