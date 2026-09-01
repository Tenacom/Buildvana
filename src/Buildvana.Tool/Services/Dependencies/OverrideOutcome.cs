// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What the override lifecycle can make of one vulnerable package of one project.
/// </summary>
internal enum OverrideOutcome
{
    /// <summary>The package is promoted to a reference of the project, at a version of its own.</summary>
    Override,

    /// <summary>
    /// The package is promoted to a reference of the project, with no version: the repository pins it
    /// centrally, at a version that is both safe and no lower than the one the project resolves.
    /// </summary>
    Promote,

    /// <summary>
    /// Nothing can be written, because no version the sources list would end the vulnerability within what
    /// the package's policy allows.
    /// </summary>
    NoFix,

    /// <summary>
    /// Nothing may be written, because a decision the repository stated itself is in the way: the project
    /// references the package directly, or its central pin is vulnerable, or its central pin is below the
    /// version the project resolves.
    /// </summary>
    Blocked,
}
