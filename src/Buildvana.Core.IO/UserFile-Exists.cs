// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO;

partial class UserFile
{
    /// <summary>
    /// Determines whether the given path refers to an existing file.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns><see langword="true"/> if <paramref name="path"/> refers to an existing file;
    /// <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// <para>Like its <see cref="File"/> counterpart, this method reports <see langword="false"/>,
    /// rather than failing, when the environment denies or fails the check.</para>
    /// </remarks>
    public static bool Exists(string path)
    {
        Guard.IsNotNullOrEmpty(path);
        return File.Exists(path);
    }
}
