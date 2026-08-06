// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using Buildvana.Core.Diagnostics;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO;

/// <summary>
/// <para>Provides file operations on user files: files whose accessibility is owned by the environment
/// (repository files, configuration files, build outputs) rather than by Buildvana itself.</para>
/// <para>Each method mirrors the corresponding <see cref="File"/> or <see cref="StreamReader"/> API,
/// translating environment-driven failures (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>,
/// <see cref="SecurityException"/>) into <see cref="BuildFailedException"/> with a message naming the operation
/// and the path, so that hosts present a clean error line instead of an unhandled-exception stack trace.</para>
/// </summary>
public static partial class UserFile
{
    private static T Read<T>(string path, Func<T> read)
    {
        Guard.IsNotNullOrEmpty(path);
        try
        {
            return read();
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not read from {path}: {e.Message}", e);
        }
    }

    private static Task<T> ReadAsync<T>(string path, Func<Task<T>> read)
    {
        // Validate before the async state machine starts, so that a bad path throws synchronously,
        // like the BCL async File methods do.
        Guard.IsNotNullOrEmpty(path);
        return ReadAsyncCore(path, read);
    }

    private static async Task<T> ReadAsyncCore<T>(string path, Func<Task<T>> read)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not read from {path}: {e.Message}", e);
        }
    }

    private static void Write(string path, Action write)
    {
        Guard.IsNotNullOrEmpty(path);
        try
        {
            write();
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not write to {path}: {e.Message}", e);
        }
    }

    private static Task WriteAsync(string path, Func<Task> write)
    {
        // Validate before the async state machine starts, so that a bad path throws synchronously,
        // like the BCL async File methods do.
        Guard.IsNotNullOrEmpty(path);
        return WriteAsyncCore(path, write);
    }

    private static async Task WriteAsyncCore(string path, Func<Task> write)
    {
        try
        {
            await write().ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not write to {path}: {e.Message}", e);
        }
    }
}
