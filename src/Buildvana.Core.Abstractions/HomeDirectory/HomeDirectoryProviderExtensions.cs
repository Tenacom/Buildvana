// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Buildvana.Core.HomeDirectory;

/// <summary>
/// Provides extension methods for <see cref="IHomeDirectoryProvider"/> instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class HomeDirectoryProviderExtensions
{
    extension(IHomeDirectoryProvider @this)
    {
        /// <summary>
        /// Resolves a path against the home directory.
        /// </summary>
        /// <param name="path">The path to resolve.</param>
        /// <returns>The absolute path of <paramref name="path"/>: <paramref name="path"/> itself, normalized,
        /// when it is absolute; its resolution against the home directory when it is relative.</returns>
        /// <remarks>
        /// <para>Unlike <see cref="Path.GetFullPath(string)"/>, the result does not depend on the process's
        /// current directory.</para>
        /// </remarks>
        public string GetFullPath(string path) => Path.GetFullPath(path, @this.HomeDirectory);

        /// <summary>
        /// Expresses a path relative to the home directory, when it is inside it.
        /// </summary>
        /// <param name="path">The path to express.</param>
        /// <param name="relativePath">When this method returns <see langword="true"/>, the path relative to
        /// the home directory, with forward slashes; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="path"/> is inside the home directory;
        /// otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// <para>The home directory itself is not inside itself, and neither is a sibling whose name merely
        /// starts with the same text.</para>
        /// <para>Paths are compared as the file system compares them: without regard to case on Windows and
        /// macOS, with regard to it elsewhere.</para>
        /// </remarks>
        public bool TryGetRelativePath(string path, [NotNullWhen(true)] out string? relativePath)
        {
            relativePath = null;
            var relative = Path.GetRelativePath(@this.HomeDirectory, Path.GetFullPath(path, @this.HomeDirectory));
            if (Path.IsPathRooted(relative) || relative == "." || relative.StartsWith("..", StringComparison.Ordinal))
            {
                return false;
            }

            relativePath = relative.Replace('\\', '/');
            return true;
        }
    }
}
