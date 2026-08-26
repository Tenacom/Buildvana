// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core.Testing;
using Buildvana.Tool.Utilities;

internal sealed class AppDirectiveEditorTests
{
    private const string FileName = "app.cs";

    [Test]
    public async Task ReadDirectives_FindsManagedDirectives_InDocumentOrder()
    {
        using var home = new TempHome();
        const string content = """
            // Example hook.
            #:sdk Buildvana.Sdk@1.0.0
            #:package Buildvana.Runtime@1.0.0-preview.2
            #:package Newtonsoft.Json
            #:property LangVersion=preview

            using System;
            """;
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([
            new AppDirective(AppDirectiveKind.Sdk, "Buildvana.Sdk", "1.0.0"),
            new AppDirective(AppDirectiveKind.Package, "Buildvana.Runtime", "1.0.0-preview.2"),
            new AppDirective(AppDirectiveKind.Package, "Newtonsoft.Json", null)]);
    }

    [Test]
    public async Task ReadDirectives_StopsAtTheFirstCodeLine()
    {
        using var home = new TempHome();
        const string content = """
            #:package Alpha@1.0.0
            using System;
            #:package Beta@2.0.0
            """;
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([new AppDirective(AppDirectiveKind.Package, "Alpha", "1.0.0")]);
    }

    [Test]
    public async Task ReadDirectives_SkipsCommentsAndBlankLines()
    {
        using var home = new TempHome();
        const string content = """
            // Line comment.

            /* single-line block comment */
            /* multi-line
               block comment */
            #:package Alpha@1.0.0
            using System;
            """;
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([new AppDirective(AppDirectiveKind.Package, "Alpha", "1.0.0")]);
    }

    // C# wants a directive first on its line (CS1040), so the SDK reads no directive after a closed block
    // comment; to it the line is code, and code ends the block.
    [Test]
    public async Task ReadDirectives_StopsAtADirectiveAfterAClosedBlockComment()
    {
        using var home = new TempHome();
        const string content = """
            /* comment */ #:package Alpha@1.0.0
            #:package Beta@2.0.0
            using System;
            """;
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadDirectives_SkipsAFileOpeningShebangLine()
    {
        using var home = new TempHome();
        const string content = """
            #!/usr/bin/env dotnet
            #:package Alpha@1.0.0
            using System;
            """;
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([new AppDirective(AppDirectiveKind.Package, "Alpha", "1.0.0")]);
    }

    // C# recognizes five line terminators (ECMA-334 §6.3.2): CR, LF, CRLF, NEL, LS, and PS. The scanner
    // must split lines on all of them, not at LF alone.
    [Test]
    public async Task ReadDirectives_AcceptsAllCSharpLineTerminators()
    {
        using var home = new TempHome();
        const string content = "// Example hook.\u0085#:package Alpha@1.0.0\u2028#:package Beta@2.0.0\u2029using System;";
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([
            new AppDirective(AppDirectiveKind.Package, "Alpha", "1.0.0"),
            new AppDirective(AppDirectiveKind.Package, "Beta", "2.0.0")]);
    }

    // The SDK's own parser matches directive kinds case-sensitively; so does the editor. The line is still
    // a directive, so it does not end the block.
    [Test]
    public async Task ReadDirectives_MatchesDirectiveKindsCaseSensitively()
    {
        using var home = new TempHome();
        const string content = """
            #:Package Alpha@1.0.0
            #:package Beta@2.0.0
            using System;
            """;
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([new AppDirective(AppDirectiveKind.Package, "Beta", "2.0.0")]);
    }

    // Mirrors the SDK's parser: the value splits at its first '@', the id trimmed at its end and the
    // version at its start.
    [Test]
    public async Task ReadDirectives_TrimsAroundTheSeparator()
    {
        using var home = new TempHome();
        var path = WriteFile(home, "#:package  Alpha @ 1.0.0\nusing System;\n");

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([new AppDirective(AppDirectiveKind.Package, "Alpha", "1.0.0")]);
    }

    // A separator with nothing after it yields empty version text, not a versionless directive — again
    // mirroring the SDK's parser.
    [Test]
    public async Task ReadDirectives_ReturnsEmptyVersionText_WhenNothingFollowsTheSeparator()
    {
        using var home = new TempHome();
        const string content = """
            #:package Alpha@
            using System;
            """;
        var path = WriteFile(home, content);

        var directives = AppDirectiveEditor.ReadDirectives(path);

        await Assert.That(directives).IsEquivalentTo([new AppDirective(AppDirectiveKind.Package, "Alpha", string.Empty)]);
    }

    [Test]
    public async Task RewriteVersions_SplicesOnlyTheVersionTexts()
    {
        using var home = new TempHome();
        const string content = """
            // Example hook.
            #:sdk Buildvana.Sdk@1.0.0
            #:package Buildvana.Runtime@1.0.0
            #:package Newtonsoft.Json

            using System;

            Console.WriteLine("#:package Fake@9.9.9");
            """;
        var path = WriteFile(home, content);
        var newVersions = new Dictionary<string, string>
        {
            ["Buildvana.Sdk"] = "1.0.1",
            ["Buildvana.Runtime"] = "1.0.1",
            ["Fake"] = "0.0.1",
        };

        var changed = AppDirectiveEditor.RewriteVersions(path, d => newVersions.GetValueOrDefault(d.Id));

        await Assert.That(changed).IsTrue();
        await Assert.That(home.ReadFile(FileName)).IsEqualTo("""
            // Example hook.
            #:sdk Buildvana.Sdk@1.0.1
            #:package Buildvana.Runtime@1.0.1
            #:package Newtonsoft.Json

            using System;

            Console.WriteLine("#:package Fake@9.9.9");
            """);
    }

    [Test]
    public async Task RewriteVersions_OffersOnlyVersionedDirectives()
    {
        using var home = new TempHome();
        const string content = """
            #:package Alpha
            #:package Beta@1.0.0
            using System;
            """;
        var path = WriteFile(home, content);
        var offered = new List<AppDirective>();

        var changed = AppDirectiveEditor.RewriteVersions(path, d =>
        {
            offered.Add(d);
            return null;
        });

        await Assert.That(changed).IsFalse();
        await Assert.That(offered).IsEquivalentTo([new AppDirective(AppDirectiveKind.Package, "Beta", "1.0.0")]);
        await Assert.That(home.ReadFile(FileName)).IsEqualTo(content);
    }

    [Test]
    public async Task RewriteVersions_LeavesTheFileAlone_WhenTheNewTextEqualsTheOld()
    {
        using var home = new TempHome();
        const string content = """
            #:package Alpha@1.0.0
            using System;
            """;
        var path = WriteFile(home, content);

        var changed = AppDirectiveEditor.RewriteVersions(path, _ => "1.0.0");

        await Assert.That(changed).IsFalse();
        await Assert.That(home.ReadFile(FileName)).IsEqualTo(content);
    }

    // A lone CR is a line terminator to C#; a scanner splitting at LF alone would read this whole file as
    // one line, take everything after the '@' as version text, and splice the code away on rewrite.
    [Test]
    public async Task RewriteVersions_PreservesCrOnlyLineEndings()
    {
        using var home = new TempHome();
        var path = Path.Combine(home.RootPath, FileName);
        const string content = "#:package Alpha@1.0.0\rusing System;\r";
        const string expected = "#:package Alpha@1.0.1\rusing System;\r";
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);

        var changed = AppDirectiveEditor.RewriteVersions(path, _ => "1.0.1");

        await Assert.That(changed).IsTrue();
        var rewritten = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        await Assert.That(rewritten).IsEqualTo(expected);
    }

    [Test]
    public async Task RewriteVersions_PreservesCrlfLineEndings()
    {
        using var home = new TempHome();
        var path = Path.Combine(home.RootPath, FileName);
        const string content = "// Example hook.\r\n#:package Alpha@1.0.0\r\nusing System;\r\n";
        const string expected = "// Example hook.\r\n#:package Alpha@1.0.1\r\nusing System;\r\n";
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);

        var changed = AppDirectiveEditor.RewriteVersions(path, _ => "1.0.1");

        await Assert.That(changed).IsTrue();
        var rewritten = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        await Assert.That(rewritten).IsEqualTo(expected);
    }

    // Hook files carry a byte order mark (StyleCop insists on one for C# sources); the rewrite must not
    // strip it — nor add one to a file that has none.
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RewriteVersions_PreservesTheByteOrderMark(bool hasByteOrderMark)
    {
        using var home = new TempHome();
        var path = Path.Combine(home.RootPath, FileName);
        const string content = """
            #:package Alpha@1.0.0
            using System;
            """;
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        await File.WriteAllBytesAsync(path, hasByteOrderMark ? [0xEF, 0xBB, 0xBF, .. contentBytes] : contentBytes).ConfigureAwait(false);

        var changed = AppDirectiveEditor.RewriteVersions(path, _ => "1.0.1");

        await Assert.That(changed).IsTrue();
        var rewrittenBytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        var hasMark = rewrittenBytes is [0xEF, 0xBB, 0xBF, ..];
        await Assert.That(hasMark).IsEqualTo(hasByteOrderMark);
    }

    private static string WriteFile(TempHome home, string content)
    {
        home.WriteFile(FileName, content);
        return Path.Combine(home.RootPath, FileName);
    }
}
