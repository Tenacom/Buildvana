// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.IO;

internal sealed class UserDirectoryTests
{
    [Test]
    public async Task CreateDirectory_CreatesAllDirectoriesInPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        try
        {
            var nested = Path.Combine(root, "a", "b");

            var info = UserDirectory.CreateDirectory(nested);

            await Assert.That(info.FullName).IsEqualTo(nested);
            await Assert.That(Directory.Exists(nested)).IsTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task CreateDirectory_WithFileAtTargetPath_Fails()
    {
        // Creating a directory where a file already exists raises IOException on every platform.
        var path = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(path, string.Empty).ConfigureAwait(false);
        try
        {
            var act = () => UserDirectory.CreateDirectory(path);

            var exception = await Assert.That(act).Throws<BuildFailedException>();
            await Assert.That(exception!.Message).Contains("Could not create directory");
            await Assert.That(exception.InnerException).IsTypeOf<IOException>();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
