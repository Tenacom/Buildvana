// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO;

/// <summary>
/// <para>A single gitignore pattern, with the matching semantics of Git's own ignore machinery.</para>
/// <para>Pattern syntax and semantics follow the format documented in
/// <see href="https://git-scm.com/docs/gitignore#_pattern_format">the gitignore reference</see>:
/// <c>*</c>, <c>?</c>, and bracket expressions never match a slash; a pattern containing a slash
/// (a trailing one aside) is anchored to the directory of its source file, while any other pattern
/// matches the last path segment at any depth; a trailing slash restricts the pattern to directories;
/// and <c>**</c> standing alone between slashes and/or pattern edges matches across directories.</para>
/// <para>A syntactically broken pattern - an unclosed bracket expression, an unknown character class,
/// a trailing unescaped backslash - parses successfully but matches nothing, which is how Git itself
/// treats it.</para>
/// </summary>
public sealed class GitIgnorePattern
{
    private readonly Regex? _regex;

    private GitIgnorePattern(string originalLine, bool isNegation, bool isDirectoryOnly, Regex? regex)
    {
        OriginalLine = originalLine;
        IsNegation = isNegation;
        IsDirectoryOnly = isDirectoryOnly;
        _regex = regex;
    }

    /// <summary>
    /// Gets the line this pattern was parsed from, verbatim.
    /// </summary>
    public string OriginalLine { get; }

    /// <summary>
    /// Gets a value indicating whether this pattern is a negation (<c>!</c> prefix):
    /// a path it matches is re-included rather than ignored.
    /// </summary>
    public bool IsNegation { get; }

    /// <summary>
    /// Gets a value indicating whether this pattern only matches directories (trailing slash).
    /// </summary>
    public bool IsDirectoryOnly { get; }

    /// <summary>
    /// Parses a line of a gitignore file, matching with Git's default case sensitivity.
    /// </summary>
    /// <param name="line">The line to parse.</param>
    /// <param name="pattern">When this method returns <see langword="true"/>, the parsed pattern.</param>
    /// <returns><see langword="true"/> if <paramref name="line"/> carries a pattern;
    /// <see langword="false"/> if it is blank or a comment.</returns>
    public static bool TryParse(string line, [MaybeNullWhen(false)] out GitIgnorePattern pattern)
        => TryParse(line, MatchCasing.CaseSensitive, out pattern);

    /// <summary>
    /// Parses a line of a gitignore file.
    /// </summary>
    /// <param name="line">The line to parse.</param>
    /// <param name="matchCasing">How matching treats character casing. Git's own default is
    /// <see cref="MatchCasing.CaseSensitive"/>; <see cref="MatchCasing.PlatformDefault"/> matches
    /// case-insensitively on Windows and macOS and case-sensitively elsewhere.</param>
    /// <param name="pattern">When this method returns <see langword="true"/>, the parsed pattern.</param>
    /// <returns><see langword="true"/> if <paramref name="line"/> carries a pattern;
    /// <see langword="false"/> if it is blank or a comment.</returns>
    public static bool TryParse(
        string line,
        MatchCasing matchCasing,
        [MaybeNullWhen(false)] out GitIgnorePattern pattern)
    {
        Guard.IsNotNull(line);
        pattern = null;
        if (line.Length == 0 || line[0] == '#')
        {
            return false;
        }

        var text = TrimUnescapedTrailingSpaces(line);
        if (text.Length == 0)
        {
            return false;
        }

        var isNegation = text[0] == '!';
        if (isNegation)
        {
            text = text[1..];
        }

        var isDirectoryOnly = text.EndsWith('/');
        if (isDirectoryOnly)
        {
            text = text[..^1];
        }

        pattern = new(line, isNegation, isDirectoryOnly, TryTranslate(text, matchCasing));
        return true;
    }

    /// <summary>
    /// Tells whether this pattern matches a path.
    /// </summary>
    /// <param name="relativePath">The path to test, relative to the directory holding the pattern's
    /// source file: slash-separated, with no leading or trailing slash.</param>
    /// <param name="isDirectory">Whether <paramref name="relativePath"/> is a directory.</param>
    /// <returns><see langword="true"/> if the pattern matches; <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// <para>Matching a directory says nothing about the paths beneath it: Git ignores the contents of
    /// an ignored directory by never descending into it, and a caller walking a tree does the same.</para>
    /// </remarks>
    public bool IsMatch(string relativePath, bool isDirectory)
    {
        Guard.IsNotNullOrEmpty(relativePath);
        if (IsDirectoryOnly && !isDirectory)
        {
            return false;
        }

        return _regex is not null && _regex.IsMatch(relativePath);
    }

    /// <inheritdoc/>
    public override string ToString() => OriginalLine;

    private static string TrimUnescapedTrailingSpaces(string line)
    {
        // Trailing spaces are part of a pattern only when backslash-escaped (see the gitignore reference).
        var end = line.Length;
        while (end > 0 && line[end - 1] == ' ')
        {
            var backslashes = 0;
            while (end - 2 - backslashes >= 0 && line[end - 2 - backslashes] == '\\')
            {
                backslashes++;
            }

            if (int.IsOddInteger(backslashes))
            {
                break;
            }

            end--;
        }

        return line[..end];
    }

    private static Regex? TryTranslate(string glob, MatchCasing matchCasing)
    {
        if (glob.Length == 0)
        {
            return null;
        }

        // A pattern containing a slash (the trailing directory marker aside, already stripped by the
        // caller) is anchored to the directory of its source file; any other pattern matches the last
        // path segment at any depth. A leading slash only anchors, so it takes no part in matching.
        var isAnchored = glob.Contains('/', StringComparison.Ordinal);
        var body = glob.StartsWith('/') ? glob[1..] : glob;
        if (body.Length == 0)
        {
            return null;
        }

        var regexBody = TryTranslateBody(body);
        if (regexBody is null)
        {
            return null;
        }

        var prefix = isAnchored ? "^" : "^(?:.*/)?";
        var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        if (IsCaseInsensitive(matchCasing))
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new(prefix + regexBody + "$", options);
    }

    private static string? TryTranslateBody(string body)
    {
        var result = new StringBuilder();
        var i = 0;
        while (i < body.Length)
        {
            var c = body[i];
            switch (c)
            {
                case '*' when i + 1 < body.Length && body[i + 1] == '*':
                {
                    // A run of two or more asterisks is special only when it stands alone between
                    // slashes and/or pattern edges; anywhere else it matches like a single asterisk.
                    var runEnd = i;
                    while (runEnd < body.Length && body[runEnd] == '*')
                    {
                        runEnd++;
                    }

                    var startsSegment = i == 0 || body[i - 1] == '/';
                    var endsSegment = runEnd == body.Length || body[runEnd] == '/';
                    if (!startsSegment || !endsSegment)
                    {
                        result.Append("[^/]*");
                        i = runEnd;
                    }
                    else if (runEnd == body.Length)
                    {
                        // Trailing "/**": everything inside, at any depth.
                        result.Append(".*");
                        i = runEnd;
                    }
                    else
                    {
                        // "**/": zero or more whole directories, the trailing slash included.
                        result.Append("(?:.*/)?");
                        i = runEnd + 1;
                    }

                    break;
                }

                case '*':
                    result.Append("[^/]*");
                    i++;
                    break;
                case '?':
                    result.Append("[^/]");
                    i++;
                    break;
                case '/':
                    result.Append('/');
                    i++;
                    break;
                case '\\':
                    if (i + 1 >= body.Length)
                    {
                        // Git gives up on a pattern whose final backslash escapes nothing.
                        return null;
                    }

                    AppendLiteral(result, body[i + 1]);
                    i += 2;
                    break;
                case '[':
                    if (!TryAppendBracketExpression(body, ref i, result))
                    {
                        return null;
                    }

                    break;
                default:
                    AppendLiteral(result, c);
                    i++;
                    break;
            }
        }

        return result.ToString();
    }

    private static bool IsCaseInsensitive(MatchCasing matchCasing) => matchCasing switch
    {
        MatchCasing.CaseSensitive => false,
        MatchCasing.CaseInsensitive => true,
        MatchCasing.PlatformDefault => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(),
        _ => throw new ArgumentOutOfRangeException(nameof(matchCasing)),
    };

    private static void AppendLiteral(StringBuilder result, char c)
        => result.Append(Regex.Escape(c.ToString()));

    private static bool TryAppendBracketExpression(string body, ref int i, StringBuilder result)
    {
        // On entry i points at '['. Bracket expressions follow fnmatch(3), per the gitignore reference:
        // '!' (or '^') first negates; ']' as the first element is literal; "[:name:]" names a POSIX
        // character class; '-' between two elements is a range. An unclosed expression or an unknown
        // class name makes the whole pattern match nothing, as it does in Git.
        var j = i + 1;
        var isNegated = j < body.Length && body[j] is '!' or '^';
        if (isNegated)
        {
            j++;
        }

        var content = new StringBuilder();
        var isFirstElement = true;
        while (true)
        {
            if (j >= body.Length)
            {
                return false;
            }

            var c = body[j];
            if (c == ']' && !isFirstElement)
            {
                j++;
                break;
            }

            isFirstElement = false;
            if (c == '[' && j + 1 < body.Length && body[j + 1] == ':')
            {
                if (!TryAppendPosixClass(body, ref j, content))
                {
                    return false;
                }

                continue;
            }

            if (!TryReadElement(body, ref j, out var low))
            {
                return false;
            }

            var isRange = j + 1 < body.Length && body[j] == '-' && body[j + 1] != ']';
            if (isRange)
            {
                j++;
                if (!TryReadElement(body, ref j, out var high))
                {
                    return false;
                }

                AppendClassChar(content, low);
                content.Append('-');
                AppendClassChar(content, high);
            }
            else
            {
                AppendClassChar(content, low);
            }
        }

        // A bracket expression never matches a slash, however it is written: a negated expression gets
        // the slash added to its exclusions, any other has it subtracted.
        if (isNegated)
        {
            result.Append("[^/").Append(content).Append(']');
        }
        else
        {
            result.Append('[').Append(content).Append("-[/]]");
        }

        i = j;
        return true;
    }

    private static bool TryAppendPosixClass(string body, ref int j, StringBuilder content)
    {
        // On entry j points at "[:". The class names and their ASCII contents are the ones Git's
        // matcher understands.
        var terminator = body.IndexOf(":]", j + 2, StringComparison.Ordinal);
        if (terminator < 0)
        {
            return false;
        }

        var characters = body[(j + 2)..terminator] switch
        {
            "alnum" => "0-9A-Za-z",
            "alpha" => "A-Za-z",
            "blank" => " \\t",
            "cntrl" => "\\x00-\\x1F\\x7F",
            "digit" => "0-9",
            "graph" => "\\x21-\\x7E",
            "lower" => "a-z",
            "print" => "\\x20-\\x7E",
            "punct" => "\\x21-\\x2F\\x3A-\\x40\\x5B-\\x60\\x7B-\\x7E",
            "space" => " \\t\\n\\v\\f\\r",
            "upper" => "A-Z",
            "xdigit" => "0-9A-Fa-f",
            _ => null,
        };
        if (characters is null)
        {
            return false;
        }

        content.Append(characters);
        j = terminator + 2;
        return true;
    }

    private static bool TryReadElement(string body, ref int j, out char element)
    {
        // A backslash escapes the next character, inside a bracket expression as well as outside.
        element = body[j];
        if (element != '\\')
        {
            j++;
            return true;
        }

        if (j + 1 >= body.Length)
        {
            return false;
        }

        element = body[j + 1];
        j += 2;
        return true;
    }

    private static void AppendClassChar(StringBuilder content, char c)
    {
        // '[' starts a nested class-subtraction group in .NET regexes, so it needs escaping along with
        // the usual suspects.
        if (c is '\\' or ']' or '^' or '-' or '[')
        {
            content.Append('\\');
        }

        content.Append(c);
    }
}
