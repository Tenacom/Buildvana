// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.IO.Gitignore;

/// <summary>
/// The outcome of evaluating a path against a gitignore pattern list.
/// </summary>
public enum GitignoreDecision
{
    /// <summary>
    /// No pattern matched; the path's fate is decided elsewhere.
    /// </summary>
    None,

    /// <summary>
    /// The last matching pattern excludes the path.
    /// </summary>
    Ignore,

    /// <summary>
    /// The last matching pattern is a negation that re-includes the path.
    /// </summary>
    Include,
}
