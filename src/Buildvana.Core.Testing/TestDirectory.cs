// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Testing;

/// <summary>
/// Provides operations on the temporary directories that tests create.
/// </summary>
public static class TestDirectory
{
    private const int MaxAttempts = 20;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Deletes a directory and all its contents, if it exists, retrying while the directory or a file in it is in use.
    /// </summary>
    /// <param name="path">The directory to delete.</param>
    /// <exception cref="IOException">The directory, or a file in it, is still in use after the last attempt.</exception>
    /// <exception cref="UnauthorizedAccessException">The directory, or a file in it, still cannot be deleted
    /// after the last attempt.</exception>
    public static void Delete(string path)
    {
        Guard.IsNotNullOrEmpty(path);

        // On Windows, an exited child process or an antivirus scan can hold a handle in the directory
        // for a while after the test is done with it.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(RetryDelay);
            }
        }
    }
}
