// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Versioning;

partial class VersionService
{
    /// <summary>
    /// Represents a version increment.
    /// </summary>
    /// <remarks>
    /// <para>Constants are defined in ascending order of importance, so that they can be compared
    /// with relational operators.</para>
    /// </remarks>
    private enum VersionIncrement
    {
        /// <summary>No version increment.</summary>
        None,

        /// <summary>Increment of the minor version.</summary>
        Minor,

        /// <summary>Increment of the major version.</summary>
        Major,
    }
}
