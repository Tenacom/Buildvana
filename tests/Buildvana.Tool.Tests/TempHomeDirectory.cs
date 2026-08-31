// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;

// A home directory of its own for one test: a temporary directory, deleted when the test is done, and the
// provider that anchors the code under test to it.
internal sealed class TempHomeDirectory : IDisposable
{
    public TempHomeDirectory() => Path = Directory.CreateTempSubdirectory("bvtest_").FullName;

    public string Path { get; }

    public IHomeDirectoryProvider Provider => new FixedHomeDirectoryProvider(Path);

    public string GetFullPath(string relativePath)
        => System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
