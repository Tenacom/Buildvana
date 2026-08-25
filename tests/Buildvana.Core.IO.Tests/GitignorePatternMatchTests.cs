// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO.Gitignore;

internal sealed class GitignorePatternMatchTests
{
    [Test]
    [Arguments("foo", "foo", true)]
    [Arguments("foo", "a/foo", true)]
    [Arguments("foo", "a/b/foo", true)]
    [Arguments("foo", "foobar", false)]
    [Arguments("foo", "foo/bar", false)] // Matching the directory "foo" prunes it; see Matches remarks.
    [Arguments("/foo", "foo", true)]
    [Arguments("/foo", "a/foo", false)]
    [Arguments("doc/frotz", "doc/frotz", true)]
    [Arguments("doc/frotz", "a/doc/frotz", false)]
    [Arguments("*.html", "foo.html", true)]
    [Arguments("*.html", "a/b/foo.html", true)]
    [Arguments("*.html", "foo.htm", false)]
    [Arguments("foo/*", "foo/bar", true)]
    [Arguments("foo/*", "foo/bar/baz", false)]
    [Arguments("foo/*", "foo", false)]
    [Arguments("fo?", "foo", true)]
    [Arguments("fo?", "fo", false)]
    [Arguments("fo?", "fooo", false)]
    [Arguments("a**b", "ab", true)]
    [Arguments("a**b", "axxb", true)]
    [Arguments("a**b", "a/b", false)]
    [Arguments("**/foo", "foo", true)]
    [Arguments("**/foo", "x/y/foo", true)]
    [Arguments("**/foo/bar", "foo/bar", true)]
    [Arguments("**/foo/bar", "x/foo/bar", true)]
    [Arguments("**/foo/bar", "foo/x/bar", false)]
    [Arguments("abc/**", "abc/x", true)]
    [Arguments("abc/**", "abc/x/y", true)]
    [Arguments("a/**/b", "a/b", true)]
    [Arguments("a/**/b", "a/x/b", true)]
    [Arguments("a/**/b", "a/x/y/b", true)]
    [Arguments("a/**/b", "x/a/b", false)]
    [Arguments("**/b/c", "b/b/c", true)] // Needs backtracking: the any-depth segment must consume the first "b".
    [Arguments("*.[oa]", "lib.a", true)]
    [Arguments("*.[oa]", "main.o", true)]
    [Arguments("*.[oa]", "main.c", false)]
    [Arguments("[a-z]oo", "foo", true)]
    [Arguments("[!a-z]oo", "Foo", true)]
    [Arguments("[!a-z]oo", "foo", false)]
    [Arguments("[]a]x", "]x", true)] // "]" right after the opener is a literal member.
    [Arguments("[]a]x", "ax", true)]
    [Arguments("[a-]x", "-x", true)] // "-" before the closer is a literal member.
    [Arguments("[a-]x", "ax", true)]
    [Arguments("[c-a]x", "cx", true)] // Descending range: matches nothing, but its first character still does.
    [Arguments("[c-a]x", "ax", false)]
    [Arguments("[[:digit:]]*", "7up", true)]
    [Arguments("[[:digit:]]*", "up", false)]
    [Arguments("[[:upper:]]oo", "Foo", true)]
    [Arguments("[[:alnum:]]x", "ax", true)]
    [Arguments("[[:alnum:]]x", "7x", true)]
    [Arguments("[[:alnum:]]x", "-x", false)]
    [Arguments("[[:alpha:]]x", "ax", true)]
    [Arguments("[[:alpha:]]x", "7x", false)]
    [Arguments("[[:blank:]]x", " x", true)]
    [Arguments("[[:blank:]]x", "\tx", true)]
    [Arguments("[[:blank:]]x", "\nx", false)]
    [Arguments("[[:cntrl:]]x", "\u0001x", true)]
    [Arguments("[[:cntrl:]]x", "\u007fx", true)]
    [Arguments("[[:cntrl:]]x", "ax", false)]
    [Arguments("[[:graph:]]x", "!x", true)]
    [Arguments("[[:graph:]]x", " x", false)] // Space is printable but not graphic.
    [Arguments("[[:graph:]]x", "\u007fx", false)]
    [Arguments("[[:lower:]]x", "ax", true)]
    [Arguments("[[:lower:]]x", "Ax", false)]
    [Arguments("[[:print:]]x", " x", true)] // Space is printable.
    [Arguments("[[:print:]]x", "\u007fx", false)]
    [Arguments("[[:punct:]]x", ",x", true)]
    [Arguments("[[:punct:]]x", "ax", false)]
    [Arguments("[[:punct:]]x", " x", false)]
    [Arguments("[[:space:]]x", " x", true)]
    [Arguments("[[:space:]]x", "\rx", true)]
    [Arguments("[[:space:]]x", "ax", false)]
    [Arguments("[[:xdigit:]]x", "fx", true)]
    [Arguments("[[:xdigit:]]x", "Fx", true)]
    [Arguments("[[:xdigit:]]x", "gx", false)]
    [Arguments("[^a-z]oo", "Foo", true)] // wildmatch.c accepts "^" as a negation marker beside "!".
    [Arguments("[^a-z]oo", "foo", false)]
    [Arguments("[\\]]x", "]x", true)] // Escaped member.
    [Arguments("[\\]]x", "ax", false)]
    [Arguments("[a-\\z]x", "mx", true)] // Escaped range endpoint.
    [Arguments("[a-\\z]x", "Ax", false)]
    [Arguments("[[:foo]x", ":x", true)] // No ":]" closes the class: "[" becomes a literal member, ":foo" follow.
    [Arguments("[[:foo]x", "fx", true)]
    [Arguments("[[:foo]x", "bx", false)]
    [Arguments("foo\\*bar", "foo*bar", true)]
    [Arguments("foo\\*bar", "fooxbar", false)]
    [Arguments("\\a", "a", true)] // gitignore(5): "\a" matches "a" even though nothing needs escaping.
    [Arguments("\\#foo", "#foo", true)]
    [Arguments("\\!important!.txt", "!important!.txt", true)]
    public async Task Matches_OnFiles_CaseSensitive(string pattern, string path, bool expected)
    {
        await Assert.That(Match(pattern, path)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("frotz/", "frotz", true)]
    [Arguments("frotz/", "a/frotz", true)] // gitignore(5): "frotz/" matches "a/frotz" that is a directory.
    [Arguments("doc/frotz/", "doc/frotz", true)]
    [Arguments("doc/frotz/", "a/doc/frotz", false)]
    [Arguments("abc/**", "abc", false)] // "Everything inside": the directory itself is not matched.
    public async Task Matches_OnDirectories(string pattern, string path, bool expected)
    {
        await Assert.That(Match(pattern, path, isDirectory: true)).IsEqualTo(expected);
    }

    [Test]
    public async Task Matches_DirectoryOnlyPattern_DoesNotMatchFile()
    {
        await Assert.That(Match("frotz/", "frotz")).IsFalse();
    }

    [Test]
    [Arguments("FOO", "foo", true)]
    [Arguments("*.HTML", "foo.html", true)]
    [Arguments("[A]x", "ax", true)] // Symmetric folding; Git's asymmetric fold would say false here.
    public async Task Matches_IgnoringCase(string pattern, string path, bool expected)
    {
        await Assert.That(Match(pattern, path, ignoreCase: true)).IsEqualTo(expected);
    }

    [Test]
    public async Task Matches_CaseSensitive_DoesNotFold()
    {
        await Assert.That(Match("FOO", "foo")).IsFalse();
    }

    [Test]
    public async Task Matches_TrailingSpaces_AreStripped()
    {
        await Assert.That(Match("foo   ", "foo")).IsTrue();
    }

    [Test]
    public async Task Matches_EscapedTrailingSpace_IsLiteral()
    {
        await Assert.That(Match("foo\\ ", "foo ")).IsTrue();
        await Assert.That(Match("foo\\ ", "foo")).IsFalse();
    }

    [Test]
    public async Task Matches_WithEmptyPath_ReturnsFalse()
    {
        var pattern = GitignorePattern.TryParse("*")!;

        await Assert.That(pattern.Matches([], isDirectory: false, ignoreCase: false)).IsFalse();
    }

    private static bool Match(string patternText, string path, bool isDirectory = false, bool ignoreCase = false)
    {
        var pattern = GitignorePattern.TryParse(patternText)!;
        return pattern.Matches(path.Split('/'), isDirectory, ignoreCase);
    }
}
