// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO;

internal sealed class GitIgnorePatternListTests
{
    [Test]
    public async Task Parse_SkipsBlankAndCommentLines()
    {
        var list = GitIgnorePatternList.Parse(["# comment", string.Empty, "*.log", "!keep.log"]);

        await Assert.That(list.Patterns.Count).IsEqualTo(2);
        await Assert.That(list.Patterns[0].OriginalLine).IsEqualTo("*.log");
        await Assert.That(list.Patterns[1].OriginalLine).IsEqualTo("!keep.log");
    }

    [Test]
    public async Task GetDecision_WithNoMatchingPattern_ReturnsNone()
    {
        var list = GitIgnorePatternList.Parse(["*.log"]);

        await Assert.That(list.GetDecision("readme.md", isDirectory: false)).IsEqualTo(GitIgnoreDecision.None);
    }

    [Test]
    public async Task GetDecision_OnEmptyList_ReturnsNone()
    {
        var list = new GitIgnorePatternList([]);

        await Assert.That(list.GetDecision("anything", isDirectory: false)).IsEqualTo(GitIgnoreDecision.None);
    }

    [Test]
    public async Task GetDecision_GivesLastMatchingPatternTheLastWord()
    {
        var list = GitIgnorePatternList.Parse(["*.log", "!important.log"]);

        await Assert.That(list.GetDecision("debug.log", isDirectory: false)).IsEqualTo(GitIgnoreDecision.Ignore);
        await Assert.That(list.GetDecision("important.log", isDirectory: false)).IsEqualTo(GitIgnoreDecision.Reinclude);
    }

    [Test]
    public async Task GetDecision_WithNegationBeforeIgnore_Ignores()
    {
        var list = GitIgnorePatternList.Parse(["!important.log", "*.log"]);

        await Assert.That(list.GetDecision("important.log", isDirectory: false)).IsEqualTo(GitIgnoreDecision.Ignore);
    }

    [Test]
    public async Task GetDecision_HonorsDirectoryOnlyPatterns()
    {
        var list = GitIgnorePatternList.Parse(["foo", "!foo/"]);

        await Assert.That(list.GetDecision("foo", isDirectory: true)).IsEqualTo(GitIgnoreDecision.Reinclude);
        await Assert.That(list.GetDecision("foo", isDirectory: false)).IsEqualTo(GitIgnoreDecision.Ignore);
    }

    [Test]
    public async Task Parse_PassesMatchCasingThrough()
    {
        var list = GitIgnorePatternList.Parse(["*.LOG"], MatchCasing.CaseInsensitive);

        await Assert.That(list.GetDecision("debug.log", isDirectory: false)).IsEqualTo(GitIgnoreDecision.Ignore);
    }

    [Test]
    public async Task Constructor_KeepsPatternOrder()
    {
        _ = GitIgnorePattern.TryParse("*.log", out var ignore);
        _ = GitIgnorePattern.TryParse("!keep.log", out var reinclude);
        var list = new GitIgnorePatternList([ignore!, reinclude!]);

        await Assert.That(list.GetDecision("keep.log", isDirectory: false)).IsEqualTo(GitIgnoreDecision.Reinclude);
    }
}
