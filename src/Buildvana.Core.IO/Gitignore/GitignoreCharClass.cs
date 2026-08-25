// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// A bracket expression from a gitignore pattern: an optionally negated set of literal characters,
/// character ranges, and POSIX named classes, matched against a single character.
/// </summary>
public sealed class GitignoreCharClass
{
    private readonly string _chars;
    private readonly (char First, char Last)[] _ranges;
    private readonly GitignoreNamedClass[] _namedClasses;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitignoreCharClass"/> class.
    /// </summary>
    /// <param name="isNegated">Whether the set is negated.</param>
    /// <param name="chars">The literal member characters, escapes already resolved.</param>
    /// <param name="ranges">The member ranges. A range whose first character is greater than its last
    /// matches nothing, as in Git.</param>
    /// <param name="namedClasses">The member POSIX named classes.</param>
    internal GitignoreCharClass(
        bool isNegated,
        string chars,
        (char First, char Last)[] ranges,
        GitignoreNamedClass[] namedClasses)
    {
        IsNegated = isNegated;
        _chars = chars;
        _ranges = ranges;
        _namedClasses = namedClasses;
    }

    /// <summary>
    /// Gets a value indicating whether the set is negated: the expression matches characters outside the set.
    /// </summary>
    public bool IsNegated { get; }

    /// <summary>
    /// Determines whether the expression matches a character.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <param name="ignoreCase"><see langword="true"/> to ignore letter case.</param>
    /// <returns><see langword="true"/> when the expression matches <paramref name="c"/>.</returns>
    /// <remarks>
    /// <para>Case-insensitive membership tests the character itself and both its invariant case foldings,
    /// so the case of neither side matters: <c>[A]</c> and <c>[a]</c> both match <c>a</c> and <c>A</c>.
    /// Git instead folds one side only
    /// (<see href="https://github.com/git/git/blob/master/wildmatch.c"><c>wildmatch.c</c></see> lowercases
    /// the path character, compares set members as written, and only retries ranges with the uppercased
    /// path character), so its <c>[A]</c> matches nothing. The symmetric test trades that corner of
    /// conformance for a rule a reader can predict.</para>
    /// </remarks>
    public bool Matches(char c, bool ignoreCase)
    {
        var isInSet = Contains(c);
        if (!isInSet && ignoreCase)
        {
            isInSet = Contains(char.ToUpperInvariant(c)) || Contains(char.ToLowerInvariant(c));
        }

        return IsNegated ? !isInSet : isInSet;
    }

    private static bool MatchesNamedClass(GitignoreNamedClass namedClass, char c)
    {
        return namedClass switch
        {
            GitignoreNamedClass.Alnum => char.IsAsciiLetterOrDigit(c),
            GitignoreNamedClass.Alpha => char.IsAsciiLetter(c),
            GitignoreNamedClass.Blank => c is ' ' or '\t',
            GitignoreNamedClass.Cntrl => c is < ' ' or '\u007f',
            GitignoreNamedClass.Digit => char.IsAsciiDigit(c),
            GitignoreNamedClass.Graph => c is > ' ' and < '\u007f',
            GitignoreNamedClass.Lower => char.IsAsciiLetterLower(c),
            GitignoreNamedClass.Print => c is >= ' ' and < '\u007f',
            GitignoreNamedClass.Punct => c is > ' ' and < '\u007f' && !char.IsAsciiLetterOrDigit(c),
            GitignoreNamedClass.Space => c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r',
            GitignoreNamedClass.Upper => char.IsAsciiLetterUpper(c),
            GitignoreNamedClass.Xdigit => char.IsAsciiHexDigit(c),
            _ => false,
        };
    }

    private bool Contains(char c)
    {
        if (_chars.Contains(c, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var (first, last) in _ranges)
        {
            if (c >= first && c <= last)
            {
                return true;
            }
        }

        foreach (var namedClass in _namedClasses)
        {
            if (MatchesNamedClass(namedClass, c))
            {
                return true;
            }
        }

        return false;
    }
}
