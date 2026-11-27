// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Versioning;

// Takes the write permission of a directory away from its owner, and gives it back on disposal.
[UnsupportedOSPlatform("windows")]
internal sealed class UnixWriteProtection : IDisposable
{
    private readonly string _path;
    private bool _disposed;

    public UnixWriteProtection(string path)
    {
        _path = path;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
