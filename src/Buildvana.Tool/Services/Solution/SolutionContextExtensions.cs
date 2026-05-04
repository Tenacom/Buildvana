// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using CommunityToolkit.Diagnostics;
using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Provides extension methods for <see cref="SolutionContext"/> instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class SolutionContextExtensions
{
    extension(SolutionContext @this)
    {
        /// <summary>
        /// Gets the absolute path of the directory containing the solution file.
        /// </summary>
        public string SolutionDirectory => Path.GetDirectoryName(@this.SolutionPath)!;

        /// <summary>
        /// Resolves a path that is relative to <see cref="SolutionDirectory"/> into an absolute path,
        /// normalizing path separators so values stored with <c>\</c> (e.g. paths read from a <c>.sln</c>)
        /// resolve correctly on Unix.
        /// </summary>
        /// <param name="relativePath">The path to resolve. Absolute paths are returned as-is, modulo full-path normalization.</param>
        /// <returns>The absolute path.</returns>
        public string ResolvePath(string relativePath)
        {
            Guard.IsNotNull(relativePath);
            var normalized = relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(@this.SolutionDirectory, normalized));
        }

        /// <summary>
        /// Resolves the absolute path of the project file represented by <paramref name="project"/>.
        /// </summary>
        /// <param name="project">The project whose file path should be resolved.</param>
        /// <returns>The absolute path of <paramref name="project"/>'s project file.</returns>
        public string ResolveProjectPath(SolutionProjectModel project)
        {
            Guard.IsNotNull(project);
            return @this.ResolvePath(project.FilePath);
        }
    }
}
