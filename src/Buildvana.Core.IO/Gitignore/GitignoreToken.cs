// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// One matching unit within a <see cref="GitignoreSegment"/>: a literal character, a wildcard,
/// or a bracket expression.
/// </summary>
public readonly record struct GitignoreToken
{
    private GitignoreToken(GitignoreTokenKind kind, char value, GitignoreCharClass? charClass)
    {
        Kind = kind;
        Value = value;
        CharClass = charClass;
    }

    /// <summary>
    /// Gets the token that matches any run of characters, the empty run included (<c>*</c>).
    /// </summary>
    public static GitignoreToken AnyRun { get; } = new(GitignoreTokenKind.AnyRun, default, null);

    /// <summary>
    /// Gets the token that matches any single character (<c>?</c>).
    /// </summary>
    public static GitignoreToken AnyChar { get; } = new(GitignoreTokenKind.AnyChar, default, null);

    /// <summary>
    /// Gets the token's kind.
    /// </summary>
    public GitignoreTokenKind Kind { get; }

    /// <summary>
    /// Gets the character a <see cref="GitignoreTokenKind.Literal"/> token matches.
    /// Escapes are already resolved: for the pattern text <c>\*</c>, this is <c>*</c>.
    /// </summary>
    public char Value { get; }

    /// <summary>
    /// Gets the bracket expression of a <see cref="GitignoreTokenKind.CharClass"/> token;
    /// <see langword="null"/> for every other kind.
    /// </summary>
    public GitignoreCharClass? CharClass { get; }

    /// <summary>
    /// Creates a token matching exactly one character.
    /// </summary>
    /// <param name="value">The character to match.</param>
    /// <returns>The literal token.</returns>
    public static GitignoreToken Literal(char value) => new(GitignoreTokenKind.Literal, value, null);

    /// <summary>
    /// Creates a token matching a single character against a bracket expression.
    /// </summary>
    /// <param name="charClass">The bracket expression.</param>
    /// <returns>The character class token.</returns>
    internal static GitignoreToken ForCharClass(GitignoreCharClass charClass)
        => new(GitignoreTokenKind.CharClass, default, charClass);
}
