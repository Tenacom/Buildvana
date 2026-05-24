// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.HomeDirectory;

/// <summary>
/// Canonical implementation of the Buildvana "home directory" discovery algorithm:
/// the nearest directory, starting at a given directory and walking upward, that contains any home marker —
/// a <c>buildvana.json</c> or <c>buildvana.jsonc</c> configuration file, a <c>.buildvana-home</c> file
/// (manual override), a <c>.git</c> file (worktree or submodule), or a <c>.git/HEAD</c> file (regular repository).
/// </summary>
/// <remarks>
/// <para>The search stops at the first directory (the start directory included) that contains any marker;
/// a configuration file only counts when it sits at that directory. Whether a configuration file is actually
/// present there — and which one — is determined separately by <c>BuildvanaConfigLoader</c>.</para>
/// <para>This algorithm mirrors the discovery performed by the Buildvana SDK in
/// <c>src/Buildvana.Sdk/Sdk/Sdk.props</c>. Any change made here MUST be applied to that file as well.</para>
/// </remarks>
public static class HomeDirectoryDiscovery
{
    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> (inclusive), looking for the nearest directory
    /// that contains a home marker.
    /// </summary>
    /// <param name="startDirectory">The directory from which to begin the search. Resolved against the current process's
    /// working directory if relative.</param>
    /// <param name="homeDirectory">When this method returns <see langword="true"/>, the absolute path of the discovered
    /// home directory, with a trailing directory separator; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a home directory was discovered; otherwise, <see langword="false"/>.</returns>
    public static bool TryDiscover(string startDirectory, [MaybeNullWhen(false)] out string homeDirectory)
    {
        Guard.IsNotNullOrEmpty(startDirectory);

        string? current = Path.GetFullPath(startDirectory);
        while (current is not null)
        {
            if (DirectoryContainsMarker(current))
            {
                homeDirectory = NormalizeDirectory(current);
                return true;
            }

            current = Path.GetDirectoryName(current);
        }

        homeDirectory = null;
        return false;
    }

    private static bool DirectoryContainsMarker(string directory)
    {
        var hasConfigFile = File.Exists(Path.Combine(directory, "buildvana.json"))
            || File.Exists(Path.Combine(directory, "buildvana.jsonc"));
        var hasManualMarker = File.Exists(Path.Combine(directory, ".buildvana-home"));

        // A regular repository has a .git directory containing HEAD; a worktree or submodule has a .git file.
        var hasGitMarker = File.Exists(Path.Combine(directory, ".git", "HEAD"))
            || File.Exists(Path.Combine(directory, ".git"));

        return hasConfigFile || hasManualMarker || hasGitMarker;
    }

    private static string NormalizeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        return full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
    }
}
