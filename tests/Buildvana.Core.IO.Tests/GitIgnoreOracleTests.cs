// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO;
using Buildvana.Core.Testing;
using LibGit2Sharp;

/// <summary>
/// Compares <see cref="GitIgnorePattern"/> decisions against Git's own, with libgit2 as the oracle:
/// each probe path is judged by <see cref="Ignore.IsPathIgnored"/> on a real repository and by a
/// pattern list parsed from the same gitignore content, and the verdicts must agree.
/// </summary>
internal sealed class GitIgnoreOracleTests
{
    private const string GitIgnoreContent = """
        *.log
        !important.log
        /build
        obj/
        doc/frotz
        **/temp
        cache/**
        a/**/b
        \#literal
        [Bb]anana
        foo[0-9]
        sub/*.txt
        bar
        !bar/
        """;

    private static readonly string[] FileProbes =
    [
        "debug.log",
        "sub/debug.log",
        "important.log",
        "sub/important.log",
        "build",
        "sub/build",
        "obj",
        "doc/frotz",
        "x/doc/frotz",
        "temp",
        "x/temp",
        "cache/x",
        "cache/deep/y",
        "a/b",
        "a/x/b",
        "ab",
        "#literal",
        "banana",
        "Banana",
        "zanana",
        "foo5",
        "foox",
        "sub/a.txt",
        "sub/deep/a.txt",
        "bar/x",
        "readme.md",
    ];

    private static readonly string[] DirectoryProbes =
    [
        "obj",
        "sub/obj",
        "build",
        "temp",
        "x/temp",
        "banana",
        "doc/frotz",
        "bar",
        "notignored",
    ];

    [Test]
    public async Task Decisions_OnFiles_AgreeWithGit()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile(".gitignore", GitIgnoreContent);
        foreach (var path in FileProbes)
        {
            CreateFile(repo.RootPath, path);
        }

        using var repository = new Repository(repo.RootPath);
        var list = ParseList(repository);
        List<string> mismatches = [];
        foreach (var path in FileProbes)
        {
            var expected = repository.Ignore.IsPathIgnored(path);
            var actual = IsIgnoredByCascade(list, path, isDirectory: false);
            if (actual != expected)
            {
                mismatches.Add($"{path}: git={expected}, ours={actual}");
            }
        }

        await Assert.That(string.Join("; ", mismatches)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Decisions_OnDirectories_AgreeWithGit()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile(".gitignore", GitIgnoreContent);
        foreach (var path in DirectoryProbes)
        {
            _ = Directory.CreateDirectory(Path.Combine(repo.RootPath, path.Replace('/', Path.DirectorySeparatorChar)));
        }

        using var repository = new Repository(repo.RootPath);
        var list = ParseList(repository);
        List<string> mismatches = [];
        foreach (var path in DirectoryProbes)
        {
            // libgit2 takes a trailing slash to mean the path is a directory.
            var expected = repository.Ignore.IsPathIgnored(path + "/");
            var actual = IsIgnoredByCascade(list, path, isDirectory: true);
            if (actual != expected)
            {
                mismatches.Add($"{path}: git={expected}, ours={actual}");
            }
        }

        await Assert.That(string.Join("; ", mismatches)).IsEqualTo(string.Empty);
    }

    private static void CreateFile(string root, string relativePath)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "x");
    }

    private static GitIgnorePatternList ParseList(Repository repository)
    {
        // Compare in the repository's own casing mode: Repository.Init sets core.ignorecase to suit
        // the filesystem, so the oracle's answers depend on it.
        var ignoreCase = repository.Config.GetValueOrDefault("core.ignorecase", false);
        var matchCasing = ignoreCase ? MatchCasing.CaseInsensitive : MatchCasing.CaseSensitive;
        return GitIgnorePatternList.Parse(GitIgnoreContent.Split('\n'), matchCasing);
    }

    private static bool IsIgnoredByCascade(GitIgnorePatternList list, string path, bool isDirectory)
    {
        // Git never descends into an ignored directory, so an ignored ancestor settles the matter
        // regardless of any decision on the path itself.
        var segments = path.Split('/');
        for (var i = 1; i < segments.Length; i++)
        {
            var ancestor = string.Join('/', segments[..i]);
            if (list.GetDecision(ancestor, isDirectory: true) == GitIgnoreDecision.Ignore)
            {
                return true;
            }
        }

        return list.GetDecision(path, isDirectory) == GitIgnoreDecision.Ignore;
    }
}
