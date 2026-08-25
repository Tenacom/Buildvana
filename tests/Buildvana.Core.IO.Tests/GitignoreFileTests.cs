// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.IO.Gitignore;

internal sealed class GitignoreFileTests
{
    [Test]
    public async Task Parse_WithNullLines_ThrowsRaw()
    {
        static GitignoreFile Act() => GitignoreFile.Parse(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Parse_SkipsLinesThatDecideNothing()
    {
        var file = GitignoreFile.Parse([string.Empty, "# comment", "*.log", "foo\\", "!keep.log"]);

        await Assert.That(file.Patterns.Count).IsEqualTo(2);
        await Assert.That(file.Patterns[0].Text).IsEqualTo("*.log");
        await Assert.That(file.Patterns[1].Text).IsEqualTo("!keep.log");
    }

    [Test]
    public async Task Evaluate_WithNoMatch_ReturnsNone()
    {
        var file = GitignoreFile.Parse(["*.log"]);

        var decision = file.Evaluate(["readme.md"], isDirectory: false, ignoreCase: false);

        await Assert.That(decision).IsEqualTo(GitignoreDecision.None);
    }

    [Test]
    public async Task Evaluate_LastMatchWins_NegationAfterIgnore()
    {
        // The example from gitignore(5): ignore generated html files, except foo.html.
        var file = GitignoreFile.Parse(["*.html", "!foo.html"]);

        var barDecision = file.Evaluate(["bar.html"], isDirectory: false, ignoreCase: false);
        var fooDecision = file.Evaluate(["foo.html"], isDirectory: false, ignoreCase: false);

        await Assert.That(barDecision).IsEqualTo(GitignoreDecision.Ignore);
        await Assert.That(fooDecision).IsEqualTo(GitignoreDecision.Include);
    }

    [Test]
    public async Task Evaluate_LastMatchWins_IgnoreAfterNegation()
    {
        var file = GitignoreFile.Parse(["!foo", "foo"]);

        var decision = file.Evaluate(["foo"], isDirectory: false, ignoreCase: false);

        await Assert.That(decision).IsEqualTo(GitignoreDecision.Ignore);
    }

    [Test]
    public async Task Evaluate_DirectoryOnlyPattern_DecidesDirectoriesOnly()
    {
        var file = GitignoreFile.Parse(["bin/"]);

        var directoryDecision = file.Evaluate(["bin"], isDirectory: true, ignoreCase: false);
        var fileDecision = file.Evaluate(["bin"], isDirectory: false, ignoreCase: false);

        await Assert.That(directoryDecision).IsEqualTo(GitignoreDecision.Ignore);
        await Assert.That(fileDecision).IsEqualTo(GitignoreDecision.None);
    }
}
