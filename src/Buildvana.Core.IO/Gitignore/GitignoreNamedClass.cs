// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// A POSIX named character class usable inside a gitignore bracket expression, e.g. <c>[[:digit:]]</c>.
/// </summary>
/// <remarks>
/// <para>Git evaluates named classes against ASCII characters only (every predicate in
/// <see href="https://github.com/git/git/blob/master/wildmatch.c"><c>wildmatch.c</c></see> is gated on
/// <c>ISASCII</c>); this implementation does the same.</para>
/// </remarks>
public enum GitignoreNamedClass
{
    /// <summary>
    /// ASCII letters and digits (<c>[:alnum:]</c>).
    /// </summary>
    Alnum,

    /// <summary>
    /// ASCII letters (<c>[:alpha:]</c>).
    /// </summary>
    Alpha,

    /// <summary>
    /// Space and tab (<c>[:blank:]</c>).
    /// </summary>
    Blank,

    /// <summary>
    /// ASCII control characters (<c>[:cntrl:]</c>).
    /// </summary>
    Cntrl,

    /// <summary>
    /// ASCII digits (<c>[:digit:]</c>).
    /// </summary>
    Digit,

    /// <summary>
    /// ASCII printable characters other than space (<c>[:graph:]</c>).
    /// </summary>
    Graph,

    /// <summary>
    /// ASCII lowercase letters (<c>[:lower:]</c>).
    /// </summary>
    Lower,

    /// <summary>
    /// ASCII printable characters, space included (<c>[:print:]</c>).
    /// </summary>
    Print,

    /// <summary>
    /// ASCII punctuation (<c>[:punct:]</c>).
    /// </summary>
    Punct,

    /// <summary>
    /// ASCII whitespace (<c>[:space:]</c>).
    /// </summary>
    Space,

    /// <summary>
    /// ASCII uppercase letters (<c>[:upper:]</c>).
    /// </summary>
    Upper,

    /// <summary>
    /// ASCII hexadecimal digits (<c>[:xdigit:]</c>).
    /// </summary>
    Xdigit,
}
