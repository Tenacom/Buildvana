// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO.Gitignore;

internal sealed class GitignorePatternParseTests
{
    [Test]
    public async Task TryParse_WithNullLine_ThrowsRaw()
    {
        static GitignorePattern? Act() => GitignorePattern.TryParse(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("#")]
    [Arguments("# comment")]
    public async Task TryParse_WithLineCarryingNoPattern_ReturnsNull(string line)
    {
        await Assert.That(GitignorePattern.TryParse(line)).IsNull();
    }

    [Test]
    [Arguments("!")] // Nothing left after the negation marker.
    [Arguments("/")] // Nothing left after anchoring.
    [Arguments("//")]
    [Arguments("a//b")] // Empty segment: no real path has one.
    [Arguments("foo\\")] // Trailing backslash never matches.
    [Arguments("[abc")] // Unclosed bracket expression.
    [Arguments("[a\\")] // Trailing backslash inside a bracket expression, at member position.
    [Arguments("[a-\\")] // Trailing backslash as a range endpoint.
    [Arguments("[[:digit")] // "[:" with no "]" anywhere after it.
    [Arguments("[]")] // "]" is a literal first member, leaving the expression unclosed.
    [Arguments("[[:bogus:]]")] // Unknown POSIX class name.
    [Arguments("x[[:digit:]")] // Named class consumed, expression never closed.
    public async Task TryParse_WithPatternThatNeverMatches_ReturnsNull(string line)
    {
        await Assert.That(GitignorePattern.TryParse(line)).IsNull();
    }

    [Test]
    public async Task TryParse_WithEscapedHash_YieldsPattern()
    {
        await Assert.That(GitignorePattern.TryParse("\\#foo")).IsNotNull();
    }

    [Test]
    public async Task TryParse_WithNegation_SetsIsNegated()
    {
        var pattern = GitignorePattern.TryParse("!foo")!;

        await Assert.That(pattern.IsNegated).IsTrue();
        await Assert.That(pattern.IsDirectoryOnly).IsFalse();
    }

    [Test]
    public async Task TryParse_WithTrailingSlash_SetsIsDirectoryOnly()
    {
        var pattern = GitignorePattern.TryParse("foo/")!;

        await Assert.That(pattern.IsDirectoryOnly).IsTrue();
        await Assert.That(pattern.IsNegated).IsFalse();
    }

    [Test]
    public async Task TryParse_PreservesOriginalText()
    {
        var pattern = GitignorePattern.TryParse("!foo/ ")!;

        await Assert.That(pattern.Text).IsEqualTo("!foo/ ");
        await Assert.That(pattern.ToString()).IsEqualTo("!foo/ ");
    }

    [Test]
    public async Task TryParse_WithUnanchoredPattern_PrependsAnyDepth()
    {
        var pattern = GitignorePattern.TryParse("foo")!;

        await Assert.That(pattern.Segments.Count).IsEqualTo(2);
        await Assert.That(pattern.Segments[0].IsAnyDepth).IsTrue();
    }

    [Test]
    public async Task TryParse_WithAnchoredPattern_DoesNotPrependAnyDepth()
    {
        var pattern = GitignorePattern.TryParse("/foo")!;

        await Assert.That(pattern.Segments.Count).IsEqualTo(1);
        await Assert.That(pattern.Segments[0].IsAnyDepth).IsFalse();
    }

    [Test]
    public async Task TryParse_WithTrailingAnyDepth_AppendsAnyComponentSegment()
    {
        var pattern = GitignorePattern.TryParse("abc/**")!;

        // "abc", "**", and the synthesized match-one-component segment.
        await Assert.That(pattern.Segments.Count).IsEqualTo(3);
        await Assert.That(pattern.Segments[1].IsAnyDepth).IsTrue();
        await Assert.That(pattern.Segments[2].IsAnyDepth).IsFalse();
    }
}
