// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Versioning;

/// <summary>
/// Specifies a kind of version increment.
/// </summary>
/// <remarks>
/// <para>The values of this enum are sorted in ascending order of importance, so that they may be compared.</para>
/// </remarks>
public enum VersionIncrement
{
    /// <summary>
    /// Represents no version advancement.
    /// </summary>
    None,

    /// <summary>
    /// Represents an increment of minor version.
    /// </summary>
    Minor,

    /// <summary>
    /// Represents an increment of major version and reset of minor version.
    /// </summary>
    Major,
}
