// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Provides helpers for dealing with file-based apps (standalone C# files executed via <c>dotnet run</c>).
/// </summary>
internal static class FileBasedAppHelper
{
    /// <summary>
    /// Computes the path of the directory where the .NET SDK stores the build artifacts of a file-based app.
    /// </summary>
    /// <param name="path">The path of the app's C# file. May be relative; resolved against the current directory.</param>
    /// <returns>The full path of the app's artifacts directory, or <see langword="null"/> when the machine has
    /// no cache root for file-based apps (see remarks). The directory may or may not exist: the SDK only creates
    /// it the first time the app is built.</returns>
    /// <remarks>
    /// <para>This method replicates the .NET SDK's own computation (<c>VirtualProjectBuilder.GetArtifactsPath</c>):
    /// a subdirectory of the user's temporary directory (on Windows) or local application data directory (elsewhere),
    /// named <c>dotnet/runfile/{name}-{hash}</c>, where <c>{name}</c> is the app's file name without extension and
    /// <c>{hash}</c> is the SHA-256 of the app file's full path, uppercased invariantly to normalize casing.</para>
    /// <para>On non-Windows machines, the local application data directory resolves to an empty string when it
    /// does not exist (e.g. a fresh container where <c>~/.local/share</c> was never created). The SDK fails every
    /// file-based app build in that state, so no cache can exist; where the SDK throws, this method returns
    /// <see langword="null"/> instead, letting callers treat the condition as "nothing has ever been cached".</para>
    /// <para>The path is computed locally, rather than obtained by evaluating the app's virtual project with
    /// <c>dotnet build --getProperty:ArtifactsPath</c>, because evaluation imports Buildvana SDK wherever a
    /// repository's <c>Directory.Build.props</c> does; callers (most notably <c>bv clean</c>) must keep working
    /// when the pinned SDK is unrestorable or the repository is otherwise in a broken state.</para>
    /// </remarks>
    public static string? GetArtifactsDirectory(string path)
    {
        Guard.IsNotNullOrEmpty(path);
        var root = OperatingSystem.IsWindows()
            ? Path.GetTempPath()
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (root.Length == 0)
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())));
        return Path.Join(root, "dotnet", "runfile", $"{Path.GetFileNameWithoutExtension(fullPath)}-{hash}");
    }
}
