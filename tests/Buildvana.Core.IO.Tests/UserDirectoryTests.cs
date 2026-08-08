// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.IO;
using Buildvana.Core.Testing;

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
            DirectoryInfo Act() => UserDirectory.CreateDirectory(path);

            var exception = await Assert.That(Act).Throws<BuildFailedException>();
            await Assert.That(exception!.Message).Contains("Could not create directory");
            await Assert.That(exception.InnerException).IsTypeOf<IOException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task CreateDirectory_WithNullPath_ThrowsRaw()
    {
        static DirectoryInfo Act() => UserDirectory.CreateDirectory(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Delete_WithEmptyDirectory_DeletesIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);

        UserDirectory.Delete(path);

        await Assert.That(Directory.Exists(path)).IsFalse();
    }

    [Test]
    public async Task Delete_WithNonEmptyDirectory_Fails()
    {
        // Deleting a non-empty directory without recursion raises IOException on every platform.
        var root = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "file.tmp"), string.Empty).ConfigureAwait(false);
        try
        {
            void Act() => UserDirectory.Delete(root);

            var exception = await Assert.That(Act).Throws<BuildFailedException>();
            await Assert.That(exception!.Message).Contains("Could not delete directory");
            await Assert.That(exception.InnerException).IsTypeOf<IOException>();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Delete_WithMissingDirectory_Fails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");

        void Act() => UserDirectory.Delete(path);

        var exception = await Assert.That(Act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not delete directory");
        await Assert.That(exception.InnerException).IsTypeOf<DirectoryNotFoundException>();
    }

    [Test]
    public async Task Delete_Recursive_WithNonEmptyDirectory_DeletesTree()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path.Combine(root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "file.tmp"), string.Empty).ConfigureAwait(false);

        UserDirectory.Delete(root, recursive: true);

        await Assert.That(Directory.Exists(root)).IsFalse();
    }

    [Test]
    public async Task Delete_WithNullPath_ThrowsRaw()
    {
        static void Act() => UserDirectory.Delete(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DeleteIfExists_WithExistingDirectory_DeletesRecursivelyAndReportsInfo()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path.Combine(root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "file.tmp"), string.Empty).ConfigureAwait(false);
        var reporter = new CaptureReporter();

        UserDirectory.DeleteIfExists(root, reporter);

        await Assert.That(Directory.Exists(root)).IsFalse();
        await Assert.That(reporter.Messages.Count).IsEqualTo(1);
        await Assert.That(reporter.Messages[0].Level).IsEqualTo(MessageLevel.Info);
        await Assert.That(reporter.Messages[0].Message).IsEqualTo($"Deleting directory: {root}");
    }

    [Test]
    public async Task DeleteIfExists_WithMissingDirectory_SkipsAndReportsDetail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        var reporter = new CaptureReporter();

        UserDirectory.DeleteIfExists(path, reporter);

        await Assert.That(reporter.Messages.Count).IsEqualTo(1);
        await Assert.That(reporter.Messages[0].Level).IsEqualTo(MessageLevel.Detail);
        await Assert.That(reporter.Messages[0].Message).IsEqualTo($"Skipping non-existent directory: {path}");
    }

    [Test]
    public async Task DeleteIfExists_WithoutReporter_DeletesDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);

        UserDirectory.DeleteIfExists(path);

        await Assert.That(Directory.Exists(path)).IsFalse();
    }

    // The null/empty guard lives in Exists, the first call DeleteIfExists makes; this test pins the
    // raw-throw contract so it survives reorderings of the implementation.
    [Test]
    public async Task DeleteIfExists_WithNullPath_ThrowsRaw()
    {
        static void Act() => UserDirectory.DeleteIfExists(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task EnumerateFiles_ReturnsAbsolutePathsOfMatchingFilesOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path.Combine(root, "sub"));
        await File.WriteAllTextAsync(Path.Combine(root, "a.nupkg"), string.Empty).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), string.Empty).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(root, "sub", "c.nupkg"), string.Empty).ConfigureAwait(false);
        try
        {
            var topLevel = UserDirectory.EnumerateFiles(root, "*.nupkg");
            var recursive = UserDirectory.EnumerateFiles(root, "**/*.nupkg");
            var none = UserDirectory.EnumerateFiles(root, "*.zip");

            string[] expectedTopLevel = [Path.Combine(root, "a.nupkg")];
            string[] expectedRecursive = [Path.Combine(root, "a.nupkg"), Path.Combine(root, "sub", "c.nupkg")];
            await Assert.That(topLevel.SequenceEqual(expectedTopLevel)).IsTrue();
            await Assert.That(recursive.Order(StringComparer.Ordinal).SequenceEqual(expectedRecursive)).IsTrue();
            await Assert.That(none.Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // Call sites (e.g. NuGetPushAllAsync's "No .nupkg files to push.") rely on this instead of pre-checking
    // existence; the behavior comes from the globbing package, so pin it here.
    [Test]
    public async Task EnumerateFiles_WithMissingBaseDirectory_ReturnsEmptyList()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");

        var result = UserDirectory.EnumerateFiles(path, "*.nupkg");

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task EnumerateFiles_WithNullBaseDirectory_ThrowsRaw()
    {
        static IReadOnlyList<string> Act() => UserDirectory.EnumerateFiles(null!, "*.nupkg");

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task EnumerateFiles_WithNullPattern_ThrowsRaw()
    {
        static IReadOnlyList<string> Act() => UserDirectory.EnumerateFiles(Path.GetTempPath(), null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Exists_WithExistingDirectory_ReturnsTrue()
        => await Assert.That(UserDirectory.Exists(Path.GetTempPath())).IsTrue();

    [Test]
    public async Task Exists_WithMissingDirectory_ReturnsFalse()
        => await Assert.That(UserDirectory.Exists(Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}"))).IsFalse();

    [Test]
    public async Task Exists_WithNullPath_ThrowsRaw()
    {
        static bool Act() => UserDirectory.Exists(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }
}
