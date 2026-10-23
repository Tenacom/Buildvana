// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Specifies the kind of a <see cref="GitRef"/>.
/// </summary>
internal enum GitRefKind
{
    /// <summary>A tag.</summary>
    Tag,

    /// <summary>A branch.</summary>
    Branch,

    /// <summary>A commit, named by its SHA.</summary>
    Commit,
}
