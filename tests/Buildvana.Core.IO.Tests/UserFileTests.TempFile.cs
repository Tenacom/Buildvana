// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

partial class UserFileTests
{
    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}.tmp");

        public void Dispose()
        {
            try
            {
                File.Delete(Path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A file left locked by a failed test must not turn teardown into the reported failure.
            }
        }
    }
}
