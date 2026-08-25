// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO;

internal sealed class GitIgnorePatternTests
{
    [Test]
    [Arguments("")]
    [Arguments("# comment")]
    [Arguments("   ")]
    public async Task TryParse_OnBlankOrCommentLine_ReturnsFalse(string line)
    {
        await Assert.That(GitIgnorePattern.TryParse(line, out _)).IsFalse();
    }

    [Test]
    public async Task TryParse_OnNegation_SetsIsNegation()
    {
        _ = GitIgnorePattern.TryParse("!foo", out var pattern);

        await Assert.That(pattern!.IsNegation).IsTrue();
        await Assert.That(pattern.IsDirectoryOnly).IsFalse();
        await Assert.That(pattern.IsMatch("foo", isDirectory: false)).IsTrue();
    }

    [Test]
    public async Task TryParse_OnTrailingSlash_SetsIsDirectoryOnly()
    {
        _ = GitIgnorePattern.TryParse("foo/", out var pattern);

        await Assert.That(pattern!.IsNegation).IsFalse();
        await Assert.That(pattern.IsDirectoryOnly).IsTrue();
    }

    [Test]
    public async Task ToString_ReturnsOriginalLine()
    {
        _ = GitIgnorePattern.TryParse("!foo/ ", out var pattern);

        await Assert.That(pattern!.OriginalLine).IsEqualTo("!foo/ ");
        await Assert.That(pattern.ToString()).IsEqualTo("!foo/ ");
    }

    [Test]
    [Arguments("foo", "foo", true)]
    [Arguments("foo", "a/foo", true)]
    [Arguments("foo", "foo/a", false)]
    [Arguments("foo", "foobar", false)]
    [Arguments("/foo", "foo", true)]
    [Arguments("/foo", "a/foo", false)]
    [Arguments("doc/frotz", "doc/frotz", true)]
    [Arguments("doc/frotz", "a/doc/frotz", false)]
    [Arguments("*.log", "debug.log", true)]
    [Arguments("*.log", "a/b/debug.log", true)]
    [Arguments("*.log", "log", false)]
    [Arguments("a?c", "abc", true)]
    [Arguments("a?c", "a/c", false)]
    [Arguments("foo/*", "foo/x", true)]
    [Arguments("foo/*", "foo/x/y", false)]
    [Arguments("[a-f]oo", "foo", true)]
    [Arguments("[a-f]oo", "goo", false)]
    [Arguments("[!a-f]oo", "goo", true)]
    [Arguments("[!a-f]oo", "foo", false)]
    [Arguments("[^a-f]oo", "goo", true)]
    [Arguments("[]]x", "]x", true)]
    [Arguments("[a-]", "-", true)]
    [Arguments("[a-]", "a", true)]
    [Arguments("[a-]", "b", false)]
    [Arguments("[[:digit:]]", "5", true)]
    [Arguments("[[:digit:]]", "x", false)]
    [Arguments("[[:alpha:]0]", "0", true)]
    [Arguments("[![:digit:]]", "x", true)]
    [Arguments("[![:digit:]]", "5", false)]
    [Arguments("**/foo", "foo", true)]
    [Arguments("**/foo", "a/b/foo", true)]
    [Arguments("**/foo", "afoo", false)]
    [Arguments("**/foo/bar", "foo/bar", true)]
    [Arguments("**/foo/bar", "a/foo/bar", true)]
    [Arguments("**/foo/bar", "foo/x/bar", false)]
    [Arguments("abc/**", "abc/x", true)]
    [Arguments("abc/**", "abc/x/y", true)]
    [Arguments("abc/**", "abc", false)]
    [Arguments("abc/**", "xabc/y", false)]
    [Arguments("a/**/b", "a/b", true)]
    [Arguments("a/**/b", "a/x/b", true)]
    [Arguments("a/**/b", "a/x/y/b", true)]
    [Arguments("a/**/b", "ab", false)]
    [Arguments("a**b", "ab", true)]
    [Arguments("a**b", "axyzb", true)]
    [Arguments("a**b", "a/b", false)]
    [Arguments(@"\*x", "*x", true)]
    [Arguments(@"\*x", "ax", false)]
    [Arguments(@"\!important!.txt", "!important!.txt", true)]
    [Arguments(@"\#file", "#file", true)]
    [Arguments("foo  ", "foo", true)]
    [Arguments(@"foo\ ", "foo ", true)]
    [Arguments(@"foo\ ", "foo", false)]
    public async Task IsMatch_OnFilePath_FollowsGitSemantics(string patternText, string path, bool expected)
    {
        await Assert.That(GitIgnorePattern.TryParse(patternText, out var pattern)).IsTrue();
        await Assert.That(pattern!.IsMatch(path, isDirectory: false)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("frotz/", "frotz", true, true)]
    [Arguments("frotz/", "frotz", false, false)]
    [Arguments("frotz/", "a/frotz", true, true)]
    [Arguments("doc/frotz/", "doc/frotz", true, true)]
    [Arguments("doc/frotz/", "a/doc/frotz", true, false)]
    public async Task IsMatch_WithTrailingSlash_MatchesDirectoriesOnly(
        string patternText,
        string path,
        bool isDirectory,
        bool expected)
    {
        _ = GitIgnorePattern.TryParse(patternText, out var pattern);

        await Assert.That(pattern!.IsMatch(path, isDirectory)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("[abc", "[abc")]
    [Arguments("[]", "]")]
    [Arguments(@"foo\", "foo")]
    [Arguments("[[:bogus:]]", "b")]
    [Arguments("!", "x")]
    [Arguments("/", "x")]
    [Arguments("foo//", "foo")]
    public async Task TryParse_OnBrokenPattern_YieldsPatternMatchingNothing(string patternText, string probePath)
    {
        await Assert.That(GitIgnorePattern.TryParse(patternText, out var pattern)).IsTrue();
        await Assert.That(pattern!.IsMatch(probePath, isDirectory: false)).IsFalse();
        await Assert.That(pattern.IsMatch(probePath, isDirectory: true)).IsFalse();
    }

    [Test]
    public async Task IsMatch_ByDefault_IsCaseSensitive()
    {
        _ = GitIgnorePattern.TryParse("Foo", out var pattern);

        await Assert.That(pattern!.IsMatch("Foo", isDirectory: false)).IsTrue();
        await Assert.That(pattern.IsMatch("foo", isDirectory: false)).IsFalse();
    }

    [Test]
    public async Task IsMatch_WithCaseInsensitive_FoldsCase()
    {
        _ = GitIgnorePattern.TryParse("[a-f]oo", MatchCasing.CaseInsensitive, out var pattern);

        await Assert.That(pattern!.IsMatch("FOO", isDirectory: false)).IsTrue();
    }

    [Test]
    public async Task IsMatch_WithPlatformDefault_FollowsPlatform()
    {
        var expected = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        _ = GitIgnorePattern.TryParse("Foo", MatchCasing.PlatformDefault, out var pattern);

        await Assert.That(pattern!.IsMatch("foo", isDirectory: false)).IsEqualTo(expected);
    }
}
