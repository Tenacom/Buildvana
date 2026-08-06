// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Diagnostics;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Buildvana.Core.IO;

/// <summary>
/// <para>Provides directory operations on user directories: directories whose accessibility is owned by the
/// environment (repository directories, output directories) rather than by Buildvana itself.</para>
/// <para>Unless documented otherwise, each method mirrors the corresponding <see cref="Directory"/> API,
/// translating environment-driven failures (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>,
/// <see cref="SecurityException"/>) into <see cref="BuildFailedException"/> with a message naming the operation
/// and the path, so that hosts present a clean error line instead of an unhandled-exception stack trace.</para>
/// </summary>
public static class UserDirectory
{
    /// <summary>
    /// Creates all directories and subdirectories in the specified path, unless they already exist.
    /// </summary>
    /// <param name="path">The directory to create.</param>
    /// <returns>A <see cref="DirectoryInfo"/> representing the directory at the specified path.</returns>
    /// <exception cref="BuildFailedException">The directory could not be created.</exception>
    public static DirectoryInfo CreateDirectory(string path)
    {
        Guard.IsNotNullOrEmpty(path);
        try
        {
            return Directory.CreateDirectory(path);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not create directory {path}: {e.Message}", e);
        }
    }

    /// <summary>
    /// Recursively deletes a directory and all its contents, if it exists; does nothing otherwise.
    /// </summary>
    /// <param name="path">The directory to delete.</param>
    /// <param name="reporter">Optional reporter. When provided, reports at <c>Info</c> on actual deletion
    /// and at <c>Detail</c> when the directory does not exist and is therefore skipped.</param>
    /// <exception cref="BuildFailedException">The directory could not be deleted.</exception>
    /// <remarks>
    /// <para>This method is a Buildvana-level convenience with no <see cref="Directory"/> counterpart.</para>
    /// <para>A directory that disappears between the existence check and the deletion counts as non-existent:
    /// the method succeeds.</para>
    /// </remarks>
    public static void DeleteIfExists(string path, IReporter? reporter = null)
    {
        if (!Exists(path))
        {
            reporter?.Detail($"Skipping non-existent directory: {path}");
            return;
        }

        reporter?.Info($"Deleting directory: {path}");
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // The directory vanished between the check and the delete; "if it exists" still holds.
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not delete directory {path}: {e.Message}", e);
        }
    }

    /// <summary>
    /// Deletes an empty directory.
    /// </summary>
    /// <param name="path">The directory to delete.</param>
    /// <exception cref="BuildFailedException">The directory could not be deleted:
    /// it does not exist, it is not empty, or the environment denied or failed the operation.</exception>
    public static void Delete(string path)
        => Delete(path, recursive: false);

    /// <summary>
    /// Deletes a directory and, if indicated, all its contents.
    /// </summary>
    /// <param name="path">The directory to delete.</param>
    /// <param name="recursive"><see langword="true"/> to also delete the directory's contents (files and
    /// subdirectories); <see langword="false"/> to fail unless the directory is empty.</param>
    /// <exception cref="BuildFailedException">The directory could not be deleted:
    /// it does not exist, it is not empty and <paramref name="recursive"/> is <see langword="false"/>,
    /// or the environment denied or failed the operation.</exception>
    public static void Delete(string path, bool recursive)
    {
        Guard.IsNotNullOrEmpty(path);
        try
        {
            Directory.Delete(path, recursive);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not delete directory {path}: {e.Message}", e);
        }
    }

    /// <summary>
    /// Enumerates the files matching a glob pattern, relative to a base directory.
    /// </summary>
    /// <param name="baseDirectory">The directory the glob is applied to. May be relative;
    /// resolved against the process working directory.</param>
    /// <param name="pattern">A glob pattern, e.g. <c>*.nupkg</c> or <c>**/PublicAPI.Shipped.txt</c>.</param>
    /// <param name="caseSensitive"><see langword="true"/> to match case-sensitively; default is case-insensitive.</param>
    /// <returns>The absolute paths of the matching files.</returns>
    /// <exception cref="BuildFailedException">The directory tree could not be walked.</exception>
    /// <remarks>
    /// <para>Unlike its <see cref="Directory"/> counterpart, this method matches glob patterns and returns
    /// a fully-materialized list, so that failures raised while walking the tree are reported at call time.</para>
    /// <para>Also unlike its <see cref="Directory"/> counterpart, a non-existent
    /// <paramref name="baseDirectory"/> yields an empty list rather than a failure.</para>
    /// </remarks>
    public static IReadOnlyList<string> EnumerateFiles(string baseDirectory, string pattern, bool caseSensitive = false)
    {
        Guard.IsNotNullOrEmpty(baseDirectory);
        Guard.IsNotNullOrEmpty(pattern);
        var matcher = new Matcher(caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(pattern);
        try
        {
            var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(baseDirectory)));
            return [.. result.Files.Select(f => Path.GetFullPath(Path.Combine(baseDirectory, f.Path)))];
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not enumerate files in {baseDirectory}: {e.Message}", e);
        }
    }

    /// <summary>
    /// Determines whether the given path refers to an existing directory.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns><see langword="true"/> if <paramref name="path"/> refers to an existing directory;
    /// <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// <para>Like its <see cref="Directory"/> counterpart, this method reports <see langword="false"/>,
    /// rather than failing, when the environment denies or fails the check.</para>
    /// </remarks>
    public static bool Exists(string path)
    {
        Guard.IsNotNullOrEmpty(path);
        return Directory.Exists(path);
    }
}
