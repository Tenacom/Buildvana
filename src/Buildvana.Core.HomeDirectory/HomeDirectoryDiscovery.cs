// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.HomeDirectory;

/// <summary>
/// Canonical implementation of the Buildvana "home directory" discovery algorithm:
/// the closest ancestor of a given start directory that contains either a <c>.buildvana-home</c>
/// marker, a <c>.git</c> entry (worktree, submodule, or regular repository), or a <c>.git/HEAD</c>
/// file (regular repository).
/// </summary>
/// <remarks>
/// <para>This algorithm mirrors the discovery performed by the Buildvana SDK in
/// <c>src/Buildvana.Sdk/Sdk/Sdk.props</c>. Any change made here MUST be applied to that file as well.</para>
/// </remarks>
public static class HomeDirectoryDiscovery
{
    private static readonly string[][] Markers =
    [
        [".buildvana-home"],
        [".git"],
        [".git", "HEAD"],
    ];

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/>, looking for the closest ancestor that
    /// satisfies the home directory discovery rules.
    /// </summary>
    /// <param name="startDirectory">The directory from which to begin the search. Resolved against the current process's
    /// working directory if relative.</param>
    /// <param name="homeDirectory">When this method returns <see langword="true"/>, the absolute path of the discovered
    /// home directory, with a trailing directory separator; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a home directory was discovered; otherwise, <see langword="false"/>.</returns>
    public static bool TryDiscover(string startDirectory, [MaybeNullWhen(false)] out string homeDirectory)
    {
        Guard.IsNotNullOrEmpty(startDirectory);

        var startPath = Path.GetFullPath(startDirectory);
        foreach (var marker in Markers)
        {
            if (TryFindAncestorContaining(startPath, marker, out homeDirectory))
            {
                return true;
            }
        }

        homeDirectory = null;
        return false;
    }

    private static bool TryFindAncestorContaining(string startPath, string[] markerParts, [MaybeNullWhen(false)] out string homeDirectory)
    {
        var current = startPath;
        while (current is not null)
        {
            var candidate = Path.Combine([current, .. markerParts]);
            if (File.Exists(candidate))
            {
                homeDirectory = NormalizeDirectory(current);
                return true;
            }

            current = Path.GetDirectoryName(current);
        }

        homeDirectory = null;
        return false;
    }

    private static string NormalizeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        return full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
    }
}
