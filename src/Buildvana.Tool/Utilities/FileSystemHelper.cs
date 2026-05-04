// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

using SysDirectory = System.IO.Directory;
using SysDirectoryInfo = System.IO.DirectoryInfo;
using SysFile = System.IO.File;
using SysPath = System.IO.Path;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Provides thin, host-agnostic file-system helpers used across the tool.
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// Returns whether a directory exists at the given path.
    /// </summary>
    public static bool DirectoryExists(string path)
    {
        Guard.IsNotNull(path);
        return SysDirectory.Exists(path);
    }

    /// <summary>
    /// Returns whether a file exists at the given path.
    /// </summary>
    public static bool FileExists(string path)
    {
        Guard.IsNotNull(path);
        return SysFile.Exists(path);
    }

    /// <summary>
    /// Recursively delete a directory and all its contents. No-op if the directory does not exist.
    /// </summary>
    /// <param name="path">The directory to delete.</param>
    /// <param name="logger">Optional logger. When provided, logs <c>Information</c> on actual deletion
    /// and <c>Debug</c> when the directory does not exist and is therefore skipped.</param>
    public static void DeleteDirectory(string path, ILogger? logger = null)
    {
        Guard.IsNotNull(path);
        if (!SysDirectory.Exists(path))
        {
            logger?.LogDebug("Skipping non-existent directory: {Path}", path);
            return;
        }

        logger?.LogInformation("Deleting directory: {Path}", path);
        SysDirectory.Delete(path, recursive: true);
    }

    /// <summary>
    /// Enumerate files matching <paramref name="pattern"/>, relative to <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="baseDirectory">The directory the glob is applied to. May be relative; resolved against the process working directory.</param>
    /// <param name="pattern">A glob pattern, e.g. <c>*.nupkg</c> or <c>**/PublicAPI.Shipped.txt</c>.</param>
    /// <param name="caseSensitive"><see langword="true"/> to match case-sensitively; default is case-insensitive.</param>
    /// <returns>The absolute paths of the matching files.</returns>
    public static IEnumerable<string> EnumerateFiles(string baseDirectory, string pattern, bool caseSensitive = false)
    {
        Guard.IsNotNull(baseDirectory);
        Guard.IsNotNull(pattern);
        var matcher = new Matcher(caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(pattern);
        var result = matcher.Execute(new DirectoryInfoWrapper(new SysDirectoryInfo(baseDirectory)));
        return result.Files.Select(f => SysPath.GetFullPath(SysPath.Combine(baseDirectory, f.Path)));
    }
}
