// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using Buildvana.Core.HomeDirectory;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Testing;

/// <summary>
/// A disposable temporary home directory (with no Git repository), for tests exercising file-based code
/// that resolves paths against a home directory.
/// </summary>
public sealed class TempHome : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("bv-test-home-");

    /// <summary>
    /// Gets the full path of the home directory.
    /// </summary>
    public string RootPath => _directory.FullName;

    /// <summary>
    /// Gets a home directory provider resolving to <see cref="RootPath"/>.
    /// </summary>
    public FixedHomeDirectoryProvider Provider => new(RootPath);

    /// <summary>
    /// Resolves a path against the home directory.
    /// </summary>
    /// <param name="name">The name of the file or directory, relative to the home directory. Forward
    /// slashes separate its components, whatever the platform.</param>
    /// <returns>The full path.</returns>
    public string GetFullPath(string name)
    {
        Guard.IsNotNull(name);
        return Path.Combine(RootPath, name.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Writes a file in the home directory, creating the directories that lead to it.
    /// </summary>
    /// <param name="name">The name of the file, relative to the home directory. Forward slashes separate
    /// its components, whatever the platform.</param>
    /// <param name="content">The content of the file.</param>
    public void WriteFile(string name, string content)
    {
        var path = GetFullPath(name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Reads a file in the home directory.
    /// </summary>
    /// <param name="name">The name of the file, relative to the home directory.</param>
    /// <returns>The content of the file.</returns>
    public string ReadFile(string name) => File.ReadAllText(Path.Combine(RootPath, name));

    /// <inheritdoc/>
    public void Dispose() => _directory.Delete(recursive: true);
}
