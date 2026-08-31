// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using Buildvana.Tool.Infrastructure;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Finds the files a repository owns: the ones a command that reads or rewrites the repository's own text
/// must see, and none of the debris a build leaves behind.
/// </summary>
internal static class RepositoryFiles
{
    // Exclusions on top of what .gitignore files dictate: bv's own outputs, anchored at the home directory,
    // plus the conventional build and dependency directories at any depth. The finder skips `.git` on its own.
    private static readonly string[] ExclusionPatterns =
    [
        "/" + CommonPaths.AllArtifacts + "/",
        "/" + CommonPaths.Scratch + "/",
        "bin/",
        "obj/",
        "node_modules/",
    ];

    /// <summary>
    /// Creates a finder over the repository's own files.
    /// </summary>
    /// <param name="home">The provider of the home directory to walk.</param>
    /// <returns>The finder.</returns>
    public static FileFinder CreateFinder(IHomeDirectoryProvider home)
    {
        Guard.IsNotNull(home);
        return new FileFinder(home.HomeDirectory, ExclusionPatterns);
    }
}
