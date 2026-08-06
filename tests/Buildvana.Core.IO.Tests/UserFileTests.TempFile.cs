// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

partial class UserFileTests
{
    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}.tmp");

        public void Dispose() => File.Delete(Path);
    }
}
