// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO;

internal sealed class FileFinderTests
{
    [Test]
    public async Task GetFiles_ReturnsRelativePathsSortedDepthFirst()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, "b.txt");
            WriteFile(root, "a/c.txt");
            WriteFile(root, "a/b/d.txt");

            var files = new FileFinder(root).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo("a/b/d.txt;a/c.txt;b.txt");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_HonorsGitignore_LastMatchWins()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, ".gitignore", "*.log\n!keep.log\n");
            WriteFile(root, "keep.log");
            WriteFile(root, "x.log");
            WriteFile(root, "y.txt");

            var files = new FileFinder(root).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo(".gitignore;keep.log;y.txt");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_DeeperGitignoreOverridesShallower()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, ".gitignore", "*.log\n");
            WriteFile(root, "a.log");
            WriteFile(root, "sub/.gitignore", "!debug.log\n");
            WriteFile(root, "sub/debug.log");
            WriteFile(root, "sub/other.log");

            var files = new FileFinder(root).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo(".gitignore;sub/.gitignore;sub/debug.log");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_PrunesIgnoredDirectory_NothingReincludesInside()
    {
        var root = CreateTempTree();
        try
        {
            // gitignore(5): it is not possible to re-include a file if a parent directory is excluded.
            WriteFile(root, ".gitignore", "sub/\n");
            WriteFile(root, "sub/.gitignore", "!keep.txt\n");
            WriteFile(root, "sub/keep.txt");

            var files = new FileFinder(root).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo(".gitignore");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_AnchorsPatternsAtTheirGitignoreDirectory()
    {
        var root = CreateTempTree();
        try
        {
            // The vmlinux example from gitignore(5): an anchored negation re-includes only in its own directory.
            WriteFile(root, ".gitignore", "vmlinux*\n");
            WriteFile(root, "vmlinux.bin");
            WriteFile(root, "arch/.gitignore", "!/vmlinux*\n");
            WriteFile(root, "arch/vmlinux.lds.S");
            WriteFile(root, "arch/sub/vmlinux.old");

            var files = new FileFinder(root).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo(".gitignore;arch/.gitignore;arch/vmlinux.lds.S");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_ExclusionsPrune_GitignoreCannotReinclude()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, ".gitignore", "!bin/\n");
            WriteFile(root, "bin/x.dll");
            WriteFile(root, "src/a.cs");

            var files = new FileFinder(root, ["bin/"]).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo(".gitignore;src/a.cs");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_ExclusionNegationsWorkWithinTheList()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, "a.tmp");
            WriteFile(root, "keep.tmp");

            var files = new FileFinder(root, ["*.tmp", "!keep.tmp"]).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo("keep.tmp");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_AnchorsExclusionsAtTheBaseDirectory()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, "artifacts/x.bin");
            WriteFile(root, "sub/artifacts/y.bin");

            var files = new FileFinder(root, ["/artifacts/"]).GetFiles();

            await Assert.That(string.Join(";", files)).IsEqualTo("sub/artifacts/y.bin");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_CaseSensitivityGovernsMatching()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, ".gitignore", "FOO.txt\n");
            WriteFile(root, "foo.txt");

            var sensitive = new FileFinder(root, caseSensitivity: CaseSensitivityMode.CaseSensitive).GetFiles();
            var insensitive = new FileFinder(root, caseSensitivity: CaseSensitivityMode.CaseInsensitive).GetFiles();

            await Assert.That(string.Join(";", sensitive)).IsEqualTo(".gitignore;foo.txt");
            await Assert.That(string.Join(";", insensitive)).IsEqualTo(".gitignore");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GetFiles_WithMissingBaseDirectory_ReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");

        var files = new FileFinder(root).GetFiles();

        await Assert.That(files.Count).IsEqualTo(0);
    }

    private static string CreateTempTree()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFile(string root, string relativePath, string content = "")
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
