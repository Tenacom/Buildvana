// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.IO;

/// <summary>
/// The decision a <see cref="GitIgnorePatternList"/> yields for a path.
/// </summary>
public enum GitIgnoreDecision
{
    /// <summary>
    /// No pattern matched the path, so sources of lower precedence, if any, decide.
    /// </summary>
    None,

    /// <summary>
    /// The path is ignored: the last matching pattern is not a negation.
    /// </summary>
    Ignore,

    /// <summary>
    /// The path is re-included: the last matching pattern is a negation.
    /// </summary>
    Reinclude,
}
