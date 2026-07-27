// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Represents a <c>MAJOR.MINOR[-]</c> version specification, as stored in a repository's version file.
/// </summary>
/// <param name="Major">The major version.</param>
/// <param name="Minor">The minor version.</param>
/// <param name="Prerelease">A value indicating whether the version line is a prerelease line.</param>
public sealed record VersionSpec(int Major, int Minor, bool Prerelease)
{
    /// <summary>
    /// Returns the canonical string representation of this version specification: <c>MAJOR.MINOR</c>,
    /// followed by <c>-</c> if <see cref="Prerelease"/> is <see langword="true"/>.
    /// </summary>
    /// <returns>The canonical string representation of this version specification.</returns>
    /// <remarks>
    /// <para>A prerelease tag is never emitted: tag text in a version file is informational and is not part of
    /// the specification. The result round-trips through parsing.</para>
    /// </remarks>
    public override string ToString() => FormattableString.Invariant($"{Major}.{Minor}{(Prerelease ? "-" : string.Empty)}");
}
