// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core;
using Buildvana.Core.IO;

internal sealed partial class UserFileTests
{
    // Opening a directory as a file raises UnauthorizedAccessException on every platform — the
    // access-denied failure mode that is not an IOException, which UserFile must still wrap
    // in BuildFailedException.
    private static string DeniedAccessPath => Path.TrimEndingDirectorySeparator(Path.GetTempPath());

    [Test]
    public Task ReadAllText_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.ReadAllText(DeniedAccessPath), "Could not read from");

    [Test]
    public Task ReadAllTextWithEncoding_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.ReadAllText(DeniedAccessPath, Encoding.UTF8), "Could not read from");

    [Test]
    public Task ReadAllTextAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.ReadAllTextAsync(DeniedAccessPath), "Could not read from");

    [Test]
    public Task ReadAllTextAsyncWithEncoding_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.ReadAllTextAsync(DeniedAccessPath, Encoding.UTF8), "Could not read from");

    [Test]
    public Task ReadAllLines_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.ReadAllLines(DeniedAccessPath), "Could not read from");

    [Test]
    public Task ReadAllLinesWithEncoding_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.ReadAllLines(DeniedAccessPath, Encoding.UTF8), "Could not read from");

    [Test]
    public Task ReadAllLinesAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.ReadAllLinesAsync(DeniedAccessPath), "Could not read from");

    [Test]
    public Task ReadAllLinesAsyncWithEncoding_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.ReadAllLinesAsync(DeniedAccessPath, Encoding.UTF8), "Could not read from");

    [Test]
    public Task ReadAllBytes_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.ReadAllBytes(DeniedAccessPath), "Could not read from");

    [Test]
    public Task ReadAllBytesAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.ReadAllBytesAsync(DeniedAccessPath), "Could not read from");

    [Test]
    public Task WriteAllText_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.WriteAllText(DeniedAccessPath, "x"), "Could not write to");

    [Test]
    public Task WriteAllTextWithEncoding_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.WriteAllText(DeniedAccessPath, "x", Encoding.UTF8), "Could not write to");

    [Test]
    public Task WriteAllTextAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.WriteAllTextAsync(DeniedAccessPath, "x"), "Could not write to");

    [Test]
    public Task WriteAllTextAsyncWithEncoding_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.WriteAllTextAsync(DeniedAccessPath, "x", Encoding.UTF8), "Could not write to");

    [Test]
    public Task WriteAllLines_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.WriteAllLines(DeniedAccessPath, ["x"]), "Could not write to");

    [Test]
    public Task WriteAllLinesWithEncoding_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.WriteAllLines(DeniedAccessPath, ["x"], Encoding.UTF8), "Could not write to");

    [Test]
    public Task WriteAllLinesAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.WriteAllLinesAsync(DeniedAccessPath, ["x"]), "Could not write to");

    [Test]
    public Task WriteAllLinesAsyncWithEncoding_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.WriteAllLinesAsync(DeniedAccessPath, ["x"], Encoding.UTF8), "Could not write to");

    [Test]
    public Task WriteAllBytes_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.WriteAllBytes(DeniedAccessPath, [1]), "Could not write to");

    [Test]
    public Task WriteAllBytesAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.WriteAllBytesAsync(DeniedAccessPath, [1]), "Could not write to");

    [Test]
    public Task AppendAllText_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.AppendAllText(DeniedAccessPath, "x"), "Could not write to");

    [Test]
    public Task AppendAllTextWithEncoding_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.AppendAllText(DeniedAccessPath, "x", Encoding.UTF8), "Could not write to");

    [Test]
    public Task AppendAllTextAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.AppendAllTextAsync(DeniedAccessPath, "x"), "Could not write to");

    [Test]
    public Task AppendAllTextAsyncWithEncoding_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.AppendAllTextAsync(DeniedAccessPath, "x", Encoding.UTF8), "Could not write to");

    [Test]
    public Task AppendAllLines_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.AppendAllLines(DeniedAccessPath, ["x"]), "Could not write to");

    [Test]
    public Task AppendAllLinesWithEncoding_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.AppendAllLines(DeniedAccessPath, ["x"], Encoding.UTF8), "Could not write to");

    [Test]
    public Task AppendAllLinesAsync_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.AppendAllLinesAsync(DeniedAccessPath, ["x"]), "Could not write to");

    [Test]
    public Task AppendAllLinesAsyncWithEncoding_WithDeniedAccess_Fails()
        => AssertDeniedAsync(() => UserFile.AppendAllLinesAsync(DeniedAccessPath, ["x"], Encoding.UTF8), "Could not write to");

    [Test]
    public Task OpenRead_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.OpenRead(DeniedAccessPath), "Could not read from");

    [Test]
    public Task OpenText_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.OpenText(DeniedAccessPath), "Could not read from");

    [Test]
    public Task OpenTextWithEncoding_WithDeniedAccess_Fails()
        => AssertDenied(() => UserFile.OpenText(DeniedAccessPath, Encoding.UTF8), "Could not read from");

    // A path with an embedded null character passes the null/empty guard but is rejected by the file
    // APIs with ArgumentException on every platform — the malformed-path family that the tier filter
    // deliberately wraps along with denied accesses.
    [Test]
    public async Task ReadAllText_WithMalformedPath_Fails()
    {
        var act = () => UserFile.ReadAllText("a\0b");

        var exception = await Assert.That(act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not read from");
        await Assert.That(exception.InnerException).IsTypeOf<ArgumentException>();
    }

    [Test]
    public async Task WriteAllText_WithMalformedPath_Fails()
    {
        var act = () => UserFile.WriteAllText("a\0b", "x");

        var exception = await Assert.That(act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("Could not write to");
        await Assert.That(exception.InnerException).IsTypeOf<ArgumentException>();
    }

    // Null and empty paths are programmer errors, not user input: the guard rejects them before
    // the guarded region, so they surface raw instead of being wrapped in BuildFailedException.
    [Test]
    public async Task ReadAllText_WithNullPath_ThrowsRaw()
    {
        var act = () => UserFile.ReadAllText(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ReadAllText_WithEmptyPath_ThrowsRaw()
    {
        var act = () => UserFile.ReadAllText(string.Empty);

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task WriteAllText_WithNullPath_ThrowsRaw()
    {
        var act = () => UserFile.WriteAllText(null!, "x");

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAllText_WithEmptyPath_ThrowsRaw()
    {
        var act = () => UserFile.WriteAllText(string.Empty, "x");

        await Assert.That(act).Throws<ArgumentException>();
    }

    // The async methods are invoked without await: the guard must throw synchronously,
    // before a task is even created, like the BCL async File methods do.
    [Test]
    public async Task ReadAllTextAsync_WithNullPath_ThrowsRawAndSynchronously()
    {
        var act = () => { _ = UserFile.ReadAllTextAsync(null!); };

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ReadAllTextAsync_WithEmptyPath_ThrowsRawAndSynchronously()
    {
        var act = () => { _ = UserFile.ReadAllTextAsync(string.Empty); };

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task WriteAllTextAsync_WithNullPath_ThrowsRawAndSynchronously()
    {
        var act = () => { _ = UserFile.WriteAllTextAsync(null!, "x"); };

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WriteAllTextAsync_WithEmptyPath_ThrowsRawAndSynchronously()
    {
        var act = () => { _ = UserFile.WriteAllTextAsync(string.Empty, "x"); };

        await Assert.That(act).Throws<ArgumentException>();
    }

    [Test]
    public async Task WriteAllText_ReadAllText_RoundTrip()
    {
        using var file = new TempFile();

        var read = Act(file.Path);

        await Assert.That(read).IsEqualTo("héllo wörld");

        static string Act(string path)
        {
            UserFile.WriteAllText(path, "héllo wörld");
            return UserFile.ReadAllText(path);
        }
    }

    [Test]
    public async Task WriteAllTextWithEncoding_ReadAllTextWithEncoding_RoundTrip()
    {
        using var file = new TempFile();

        var read = Act(file.Path);

        await Assert.That(read).IsEqualTo("héllo wörld");

        static string Act(string path)
        {
            UserFile.WriteAllText(path, "héllo wörld", Encoding.Latin1);
            return UserFile.ReadAllText(path, Encoding.Latin1);
        }
    }

    [Test]
    public async Task WriteAllTextAsync_ReadAllTextAsync_RoundTrip()
    {
        using var file = new TempFile();

        await UserFile.WriteAllTextAsync(file.Path, "héllo wörld").ConfigureAwait(false);

        await Assert.That(await UserFile.ReadAllTextAsync(file.Path).ConfigureAwait(false)).IsEqualTo("héllo wörld");
    }

    [Test]
    public async Task WriteAllLines_ReadAllLines_RoundTrip()
    {
        using var file = new TempFile();
        string[] lines = ["first", "sécond", "third"];

        var read = Act(file.Path, lines);

        await Assert.That(read.SequenceEqual(lines)).IsTrue();

        static string[] Act(string path, string[] lines)
        {
            UserFile.WriteAllLines(path, lines);
            return UserFile.ReadAllLines(path);
        }
    }

    [Test]
    public async Task WriteAllLinesWithEncoding_ReadAllLinesWithEncoding_RoundTrip()
    {
        using var file = new TempFile();
        string[] lines = ["first", "sécond", "third"];

        var read = Act(file.Path, lines);

        await Assert.That(read.SequenceEqual(lines)).IsTrue();

        static string[] Act(string path, string[] lines)
        {
            UserFile.WriteAllLines(path, lines, Encoding.Latin1);
            return UserFile.ReadAllLines(path, Encoding.Latin1);
        }
    }

    [Test]
    public async Task WriteAllLinesAsync_ReadAllLinesAsync_RoundTrip()
    {
        using var file = new TempFile();
        string[] lines = ["first", "sécond", "third"];

        await UserFile.WriteAllLinesAsync(file.Path, lines).ConfigureAwait(false);

        var read = await UserFile.ReadAllLinesAsync(file.Path).ConfigureAwait(false);
        await Assert.That(read.SequenceEqual(lines)).IsTrue();
    }

    [Test]
    public async Task WriteAllBytes_ReadAllBytes_RoundTrip()
    {
        using var file = new TempFile();
        byte[] bytes = [1, 2, 3, 0xFF];

        var read = Act(file.Path, bytes);

        await Assert.That(read.SequenceEqual(bytes)).IsTrue();

        static byte[] Act(string path, byte[] bytes)
        {
            UserFile.WriteAllBytes(path, bytes);
            return UserFile.ReadAllBytes(path);
        }
    }

    [Test]
    public async Task WriteAllBytesAsync_ReadAllBytesAsync_RoundTrip()
    {
        using var file = new TempFile();
        byte[] bytes = [1, 2, 3, 0xFF];

        await UserFile.WriteAllBytesAsync(file.Path, bytes).ConfigureAwait(false);

        var read = await UserFile.ReadAllBytesAsync(file.Path).ConfigureAwait(false);
        await Assert.That(read.SequenceEqual(bytes)).IsTrue();
    }

    [Test]
    public async Task AppendAllText_AppendsToExistingContent()
    {
        using var file = new TempFile();

        var read = Act(file.Path);

        await Assert.That(read).IsEqualTo("first+second");

        static string Act(string path)
        {
            UserFile.WriteAllText(path, "first");
            UserFile.AppendAllText(path, "+second");
            return UserFile.ReadAllText(path);
        }
    }

    [Test]
    public async Task AppendAllTextAsync_AppendsToExistingContent()
    {
        using var file = new TempFile();
        await UserFile.WriteAllTextAsync(file.Path, "first").ConfigureAwait(false);

        await UserFile.AppendAllTextAsync(file.Path, "+second").ConfigureAwait(false);

        await Assert.That(await UserFile.ReadAllTextAsync(file.Path).ConfigureAwait(false)).IsEqualTo("first+second");
    }

    [Test]
    public async Task AppendAllLines_AppendsToExistingLines()
    {
        using var file = new TempFile();
        string[] expected = ["first", "second"];

        var read = Act(file.Path);

        await Assert.That(read.SequenceEqual(expected)).IsTrue();

        static string[] Act(string path)
        {
            UserFile.WriteAllLines(path, ["first"]);
            UserFile.AppendAllLines(path, ["second"]);
            return UserFile.ReadAllLines(path);
        }
    }

    [Test]
    public async Task AppendAllLinesAsync_AppendsToExistingLines()
    {
        using var file = new TempFile();
        string[] expected = ["first", "second"];
        await UserFile.WriteAllLinesAsync(file.Path, ["first"]).ConfigureAwait(false);

        await UserFile.AppendAllLinesAsync(file.Path, ["second"]).ConfigureAwait(false);

        var read = await UserFile.ReadAllLinesAsync(file.Path).ConfigureAwait(false);
        await Assert.That(read.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task OpenRead_ReadsFileContents()
    {
        using var file = new TempFile();
        byte[] bytes = [1, 2, 3];

        var read = Act(file.Path, bytes);

        await Assert.That(read.SequenceEqual(bytes)).IsTrue();

        static byte[] Act(string path, byte[] bytes)
        {
            UserFile.WriteAllBytes(path, bytes);
            using var stream = UserFile.OpenRead(path);
            var read = new byte[stream.Length];
            stream.ReadExactly(read);
            return read;
        }
    }

    [Test]
    public async Task OpenText_ReadsFileContents()
    {
        using var file = new TempFile();

        var read = Act(file.Path);

        await Assert.That(read).IsEqualTo("héllo wörld");

        static string Act(string path)
        {
            UserFile.WriteAllText(path, "héllo wörld");
            using var reader = UserFile.OpenText(path);
            return reader.ReadToEnd();
        }
    }

    [Test]
    public async Task OpenTextWithEncoding_HonorsEncoding()
    {
        using var file = new TempFile();

        var read = Act(file.Path);

        await Assert.That(read).IsEqualTo("héllo wörld");

        static string Act(string path)
        {
            UserFile.WriteAllText(path, "héllo wörld", Encoding.Latin1);
            using var reader = UserFile.OpenText(path, Encoding.Latin1);
            return reader.ReadToEnd();
        }
    }

    [Test]
    public async Task OpenTextWithEncoding_DetectsByteOrderMark()
    {
        using var file = new TempFile();

        var read = Act(file.Path);

        await Assert.That(read).IsEqualTo("héllo wörld");

        static string Act(string path)
        {
            // File.WriteAllText emits the BOM for UTF-16, which the strict UTF-8 reader must detect and honor.
            UserFile.WriteAllText(path, "héllo wörld", Encoding.Unicode);
            using var reader = UserFile.OpenText(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }

    [Test]
    public async Task OpenTextWithEncoding_WithoutByteOrderMarkDetection_ReadsByteOrderMarkAsContent()
    {
        using var file = new TempFile();

        var read = Act(file.Path);

        await Assert.That(read).IsEqualTo((char)0xFEFF + "héllo wörld");

        static string Act(string path)
        {
            // Emit an explicit UTF-8 BOM, then read with detection off and a preamble-free encoding:
            // the BOM must come back as content instead of being consumed.
            UserFile.WriteAllText(path, "héllo wörld", new UTF8Encoding(true));
            using var reader = UserFile.OpenText(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
            return reader.ReadToEnd();
        }
    }

    [Test]
    public async Task Exists_WithExistingFile_ReturnsTrue()
    {
        using var file = new TempFile();

        var exists = Act(file.Path);

        await Assert.That(exists).IsTrue();

        static bool Act(string path)
        {
            UserFile.WriteAllText(path, "x");
            return UserFile.Exists(path);
        }
    }

    [Test]
    public async Task Exists_WithMissingFile_ReturnsFalse()
    {
        using var file = new TempFile();

        await Assert.That(UserFile.Exists(file.Path)).IsFalse();
    }

    [Test]
    public async Task Exists_WithDirectoryAtPath_ReturnsFalse()
        => await Assert.That(UserFile.Exists(DeniedAccessPath)).IsFalse();

    [Test]
    public async Task Exists_WithNullPath_ThrowsRaw()
    {
        var act = () => UserFile.Exists(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Exists_WithEmptyPath_ThrowsRaw()
    {
        var act = () => UserFile.Exists(string.Empty);

        await Assert.That(act).Throws<ArgumentException>();
    }

    private static async Task AssertDenied(Action act, string expectedMessagePrefix)
    {
        var exception = await Assert.That(act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains(expectedMessagePrefix);
        await Assert.That(exception.InnerException).IsTypeOf<UnauthorizedAccessException>();
    }

    private static async Task AssertDeniedAsync(Func<Task> act, string expectedMessagePrefix)
    {
        var exception = await Assert.That(act).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains(expectedMessagePrefix);
        await Assert.That(exception.InnerException).IsTypeOf<UnauthorizedAccessException>();
    }
}
