// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Buildvana.Tool.Utilities;

partial class CakeContextExtensions
{
    /// <summary>
    /// Delete a directory, including its contents, if it exists.
    /// </summary>
    /// <param name="this">The Cake context.</param>
    /// <param name="directory">The directory to delete.</param>
    public static void DeleteDirectoryIfExists(this ICakeContext @this, DirectoryPath directory)
    {
        if (!@this.DirectoryExists(directory))
        {
            @this.Verbose($"Skipping non-existent directory: {directory}");
            return;
        }

        @this.Information($"Deleting directory: {directory}");
        @this.DeleteDirectory(directory, new() { Force = false, Recursive = true });
    }
}
