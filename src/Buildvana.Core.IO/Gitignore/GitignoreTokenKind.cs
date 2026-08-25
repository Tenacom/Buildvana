// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// The kind of a <see cref="GitignoreToken"/>.
/// </summary>
public enum GitignoreTokenKind
{
    /// <summary>
    /// Matches exactly the token's <see cref="GitignoreToken.Value"/> character.
    /// </summary>
    Literal,

    /// <summary>
    /// Matches any run of characters, the empty run included, within one path component (<c>*</c>).
    /// </summary>
    AnyRun,

    /// <summary>
    /// Matches any single character (<c>?</c>).
    /// </summary>
    AnyChar,

    /// <summary>
    /// Matches a single character against the token's <see cref="GitignoreToken.CharClass"/> (<c>[...]</c>).
    /// </summary>
    CharClass,
}
