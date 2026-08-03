// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

partial class JsonHelperTests
{
    private sealed class TempJsonFile : IDisposable
    {
        public TempJsonFile(string content)
        {
            File.WriteAllText(Path, content);
        }

        public TempJsonFile(byte[] content)
        {
            File.WriteAllBytes(Path, content);
        }

        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}.json");

        public string ReadText() => File.ReadAllText(Path);

        public void Dispose() => File.Delete(Path);
    }
}
