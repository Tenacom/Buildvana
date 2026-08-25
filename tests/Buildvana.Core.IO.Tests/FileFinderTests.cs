// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO;
using Buildvana.Core.Testing;
using LibGit2Sharp;

internal sealed class FileFinderTests
{
    [Test]
    public async Task FindFiles_ReturnsSlashSeparatedPaths_FilesBeforeSubdirectories()
    {
        using var home = new TempHome();
        CreateFile(home.RootPath, "z.txt");
        CreateFile(home.RootPath, "a.txt");
        CreateFile(home.RootPath, "m/x.txt");
        CreateFile(home.RootPath, "b/y.txt");

        var result = string.Join("|", CreateFinder(home.RootPath).FindFiles());

        await Assert.That(result).IsEqualTo("a.txt|z.txt|b/y.txt|m/x.txt");
    }

    [Test]
    public async Task FindFiles_HonorsGitIgnore_WithoutAGitRepository()
    {
        using var home = new TempHome();
        WriteFile(home.RootPath, ".gitignore", "*.log");
        CreateFile(home.RootPath, "debug.log");
        CreateFile(home.RootPath, "readme.md");

        var result = string.Join("|", CreateFinder(home.RootPath).FindFiles());

        await Assert.That(result).IsEqualTo(".gitignore|readme.md");
    }

    [Test]
    public async Task FindFiles_HonorsNestedGitIgnore_DeeperListWinning()
    {
        using var home = new TempHome();
        WriteFile(home.RootPath, ".gitignore", "*.log");
        WriteFile(home.RootPath, "sub/.gitignore", "!keep.log");
        CreateFile(home.RootPath, "debug.log");
        CreateFile(home.RootPath, "sub/debug.log");
        CreateFile(home.RootPath, "sub/keep.log");

        var result = string.Join("|", CreateFinder(home.RootPath).FindFiles());

        await Assert.That(result).IsEqualTo(".gitignore|sub/.gitignore|sub/keep.log");
    }

    [Test]
    public async Task FindFiles_NeverEntersAnIgnoredDirectory()
    {
        using var home = new TempHome();
        WriteFile(home.RootPath, ".gitignore", "logs/");
        WriteFile(home.RootPath, "logs/.gitignore", "!kept.txt");
        CreateFile(home.RootPath, "logs/kept.txt");
        CreateFile(home.RootPath, "top.txt");

        var result = string.Join("|", CreateFinder(home.RootPath).FindFiles());

        await Assert.That(result).IsEqualTo(".gitignore|top.txt");
    }

    [Test]
    public async Task FindFiles_SkipsExcludedNames_AtEveryDepth_FilesIncluded()
    {
        using var home = new TempHome();
        CreateFile(home.RootPath, "bin/x.txt");
        CreateFile(home.RootPath, "sub/bin/y.txt");
        CreateFile(home.RootPath, "sub/ok.txt");
        CreateFile(home.RootPath, "sub2/bin");

        var finder = CreateFinder(home.RootPath, excludedNames: ["bin"]);
        var result = string.Join("|", finder.FindFiles());

        await Assert.That(result).IsEqualTo("sub/ok.txt");
    }

    [Test]
    public async Task FindFiles_SkipsExcludedRootPaths_OnlyAtTheRoot()
    {
        using var home = new TempHome();
        CreateFile(home.RootPath, "artifacts/x.txt");
        CreateFile(home.RootPath, "sub/artifacts/y.txt");

        var finder = CreateFinder(home.RootPath, excludedRootPaths: ["artifacts"]);
        var result = string.Join("|", finder.FindFiles());

        await Assert.That(result).IsEqualTo("sub/artifacts/y.txt");
    }

    [Test]
    public async Task FindFiles_WithCaseInsensitiveCasing_AppliesItThroughout()
    {
        using var home = new TempHome();
        WriteFile(home.RootPath, ".gitignore", "*.LOG");
        CreateFile(home.RootPath, "Bin/x.txt");
        CreateFile(home.RootPath, "debug.log");
        CreateFile(home.RootPath, "a.txt");

        var finder = CreateFinder(home.RootPath, excludedNames: ["bin"], matchCasing: MatchCasing.CaseInsensitive);

        await Assert.That(string.Join("|", finder.FindFiles())).IsEqualTo(".gitignore|a.txt");
        await Assert.That(string.Join("|", finder.FindFiles("**/*.TXT"))).IsEqualTo("a.txt");
    }

    [Test]
    public async Task FindFiles_WithGlob_FiltersByRootRelativePath()
    {
        using var home = new TempHome();
        CreateFile(home.RootPath, "src/a.txt");
        CreateFile(home.RootPath, "src/b.md");
        CreateFile(home.RootPath, "other/c.txt");

        var result = string.Join("|", CreateFinder(home.RootPath).FindFiles("src/*.txt"));

        await Assert.That(result).IsEqualTo("src/a.txt");
    }

    [Test]
    public async Task FindFiles_WithNonMatchingGlob_ReturnsEmpty()
    {
        using var home = new TempHome();
        CreateFile(home.RootPath, "a.txt");

        var result = CreateFinder(home.RootPath).FindFiles("**/*.nope");

        await Assert.That(result.Any()).IsFalse();
    }

    [Test]
    public async Task FindFiles_WithEmptyGlob_ThrowsAtCallTime()
    {
        using var home = new TempHome();
        var finder = CreateFinder(home.RootPath);

        await Assert.That(() => finder.FindFiles(string.Empty)).Throws<ArgumentException>();
    }

    [Test]
    public async Task FindFiles_AgreesWithGitStatus()
    {
        // No negation in the nested file: libgit2 does not honor a deeper gitignore's negation against a
        // parent's pattern, while git itself does (verified with `git check-ignore -v`), so that behavior
        // cannot be oracle-tested here. FindFiles_HonorsNestedGitIgnore_DeeperListWinning covers it.
        const string rootGitIgnore = """
            *.log
            !important.log
            build/
            temp*
            """;
        const string subGitIgnore = """
            data/
            *.txt
            """;
        using var repo = new TempGitRepo();
        repo.WriteFile(".gitignore", rootGitIgnore);
        WriteFile(repo.RootPath, "sub/.gitignore", subGitIgnore);
        CreateFile(repo.RootPath, "debug.log");
        CreateFile(repo.RootPath, "important.log");
        CreateFile(repo.RootPath, "notes.md");
        CreateFile(repo.RootPath, "build/x.txt");
        CreateFile(repo.RootPath, "temperature.txt");
        CreateFile(repo.RootPath, "sub/debug.log");
        CreateFile(repo.RootPath, "sub/notes.txt");
        CreateFile(repo.RootPath, "sub/data/y.txt");
        CreateFile(repo.RootPath, "sub/inner/z.txt");

        using var repository = new Repository(repo.RootPath);
        var ignoreCase = repository.Config.GetValueOrDefault("core.ignorecase", false);
        var matchCasing = ignoreCase ? MatchCasing.CaseInsensitive : MatchCasing.CaseSensitive;
        var finder = new FileFinder(repo.RootPath, [], [".git"], matchCasing);
        var options = new StatusOptions
        {
            IncludeIgnored = false,
            IncludeUntracked = true,
            RecurseUntrackedDirs = true,
        };

        var actual = string.Join("|", finder.FindFiles().Order(StringComparer.Ordinal));
        var expected = string.Join(
            "|",
            repository.RetrieveStatus(options).Select(static x => x.FilePath).Order(StringComparer.Ordinal));

        await Assert.That(actual).IsEqualTo(expected);
    }

    private static FileFinder CreateFinder(
        string rootPath,
        IEnumerable<string>? excludedRootPaths = null,
        IEnumerable<string>? excludedNames = null,
        MatchCasing matchCasing = MatchCasing.CaseSensitive)
        => new(rootPath, excludedRootPaths ?? [], excludedNames ?? [], matchCasing);

    private static void CreateFile(string root, string relativePath)
        => WriteFile(root, relativePath, "x");

    private static void WriteFile(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
