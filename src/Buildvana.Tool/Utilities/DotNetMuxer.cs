// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Locates the <c>dotnet</c> muxer executable.
/// </summary>
internal static class DotNetMuxer
{
    /// <summary>
    /// Gets the path of the <c>dotnet</c> executable to invoke for child processes.
    /// </summary>
    /// <remarks>
    /// <para>The muxer sets <c>DOTNET_HOST_PATH</c> to the full path of the dotnet executable that launched
    /// the current process, so that exact host is re-invoked instead of relying on <c>dotnet</c> being on
    /// the PATH.</para>
    /// </remarks>
    public static string Path { get; }
        = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p ? p : "dotnet";
}
