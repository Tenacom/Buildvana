// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security;
using Buildvana.Core.Diagnostics;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO;

/// <summary>
/// <para>Provides directory operations on user directories: directories whose accessibility is owned by the
/// environment (repository directories, output directories) rather than by Buildvana itself.</para>
/// <para>Each method mirrors the corresponding <see cref="Directory"/> API, translating environment-driven
/// failures (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>, <see cref="SecurityException"/>)
/// into <see cref="BuildFailedException"/> with a message naming the operation and the path, so that hosts
/// present a clean error line instead of an unhandled-exception stack trace.</para>
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
}
