// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hosts;

/// <summary>
/// Converts the path of a file of a repository to the path a file URL of a Git host ends with.
/// </summary>
internal static class RepositoryFilePath
{
    /// <summary>
    /// Converts the path of a file of a repository to the path a file URL ends with.
    /// </summary>
    /// <param name="path">The path of the file, relative to the root of the repository.</param>
    /// <returns><paramref name="path"/>, with forward slashes as separators.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty, rooted, or holds a <c>..</c>
    /// segment.</exception>
    public static string ToUrlPath(string path)
    {
        Guard.IsNotNullOrEmpty(path);

        // Normalize to forward slashes for the URL, then reject paths that are rooted or escape the repo.
        // A rooted path is checked after normalization, so that a leading slash of either kind is rejected on
        // every platform: Path.IsPathFullyQualified is true for "/docs/x.md" on Linux and false on Windows,
        // where a fully qualified path needs a drive or a UNC share, and a backslash is a separator only there.
        // Every ".." segment must go, not just a leading one: Uri collapses parent segments as it parses,
        // so enough of them anywhere in the path walk out of the repository and even out of the owner
        // (".../blob/main/docs/../../../../../../etc/passwd" parses to "https://github.com/etc/passwd").
        var remotePath = path.Replace('\\', '/');
        Guard.IsTrue(!Path.IsPathRooted(remotePath), nameof(path), "A path must be relative to be converted to a file URL.");
        var hasParentSegment = remotePath == ".."
            || remotePath.StartsWith("../", StringComparison.Ordinal)
            || remotePath.EndsWith("/..", StringComparison.Ordinal)
            || remotePath.Contains("/../", StringComparison.Ordinal);
        Guard.IsTrue(!hasParentSegment, nameof(path), "Only a path to a file in the repository can be converted to a file URL.");
        return remotePath;
    }
}
