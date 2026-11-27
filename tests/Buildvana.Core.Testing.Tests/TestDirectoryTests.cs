// Copyright (C) Tenacom and contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Buildvana.Core.Testing;

internal sealed class TestDirectoryTests
{
    private const string HeldDirectoryName = "held";

    private const string HeldFileName = "file.txt";

    [Test]
    public async Task Delete_WhenTheDirectoryIsReleasedDuringTheRetries_DeletesIt()
    {
        var path = CreateDirectory();
        var hold = Hold(path);
        try
        {
            var releasing = ReleaseAfterDelayAsync(hold);

            TestDirectory.Delete(path);
            await releasing.ConfigureAwait(false);

            await Assert.That(Directory.Exists(path)).IsFalse();
        }
        finally
        {
            hold.Dispose();
        }
    }

    [Test]
    public async Task Delete_WhenTheDirectoryStaysInUse_ThrowsAfterTheLastAttempt()
    {
        var path = CreateDirectory();
        try
        {
            using (Hold(path))
            {
                var stopwatch = Stopwatch.StartNew();

                var exception = Assert.Throws<Exception>(() => TestDirectory.Delete(path));

                await Assert.That(exception is IOException or UnauthorizedAccessException).IsTrue();

                // 20 attempts wait 19 times 250 ms in between.
                await Assert.That(stopwatch.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(4));
            }
        }
        finally
        {
            TestDirectory.Delete(path);
        }
    }

    [Test]
    public async Task Delete_WhenTheDirectoryDoesNotExist_DoesNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");

        await Assert.That(() => TestDirectory.Delete(path)).ThrowsNothing();
        await Assert.That(Directory.Exists(path)).IsFalse();
    }

    private static string CreateDirectory()
    {
        var path = Directory.CreateTempSubdirectory("bv-test-").FullName;
        var heldDirectory = Directory.CreateDirectory(Path.Combine(path, HeldDirectoryName));
        File.WriteAllText(Path.Combine(heldDirectory.FullName, HeldFileName), string.Empty);
        return path;
    }

    // Windows refuses to delete a file that a process holds open. Linux deletes such a file, but refuses to
    // delete a file in a directory the process cannot write to.
    private static IDisposable Hold(string path)
    {
        var heldDirectory = Path.Combine(path, HeldDirectoryName);
        return OperatingSystem.IsWindows()
            ? new FileStream(Path.Combine(heldDirectory, HeldFileName), FileMode.Open, FileAccess.Read, FileShare.None)
            : new UnixWriteProtection(heldDirectory);
    }

    private static async Task ReleaseAfterDelayAsync(IDisposable hold)
    {
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        hold.Dispose();
    }
}
