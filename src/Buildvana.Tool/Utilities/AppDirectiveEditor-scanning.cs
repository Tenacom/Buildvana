// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Tool.Utilities;

partial class AppDirectiveEditor
{
    // Scans the leading directive block of a file-based app's text for managed directives.
    private static List<DirectiveMatch> Scan(string text)
    {
        var matches = new List<DirectiveMatch>();
        var inBlockComment = false;
        var isFirstLine = true;
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var newline = text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? text.Length : newline;
            if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
            {
                lineEnd--;
            }

            if (!ProcessLine(text, lineStart, lineEnd, isFirstLine, directivesAllowed: true, ref inBlockComment, matches))
            {
                break;
            }

            isFirstLine = false;
            lineStart = newline < 0 ? text.Length : newline + 1;
        }

        return matches;
    }

    // Handles one line (or, on recursion, the rest of a line after a closed block comment). Returns false
    // when the line ends the directive block: the first line that is neither blank, nor a comment, nor a
    // "#:" directive, nor the file-opening shebang. After a closed block comment only blank text or
    // another comment continues the block: C# wants a directive first on its line (CS1040), so the SDK
    // reads no directive there either, and to it the line is code.
    private static bool ProcessLine(
        string text,
        int start,
        int end,
        bool isFirstLine,
        bool directivesAllowed,
        ref bool inBlockComment,
        List<DirectiveMatch> matches)
    {
        var s = start;
        while (s < end && char.IsWhiteSpace(text[s]))
        {
            s++;
        }

        var e = end;
        while (e > s && char.IsWhiteSpace(text[e - 1]))
        {
            e--;
        }

        if (inBlockComment)
        {
            var close = FindInLine(text, s, e, "*/");
            if (close < 0)
            {
                return true;
            }

            inBlockComment = false;
            return ProcessLine(text, close + 2, e, isFirstLine: false, directivesAllowed: false, ref inBlockComment, matches);
        }

        if (s == e)
        {
            return true;
        }

        if (isFirstLine && s == 0 && HasAt(text, 0, "#!"))
        {
            return true;
        }

        if (HasAt(text, s, "//"))
        {
            return true;
        }

        if (HasAt(text, s, "/*"))
        {
            var close = FindInLine(text, s + 2, e, "*/");
            if (close < 0)
            {
                inBlockComment = true;
                return true;
            }

            return ProcessLine(text, close + 2, e, isFirstLine: false, directivesAllowed: false, ref inBlockComment, matches);
        }

        if (HasAt(text, s, "#:"))
        {
            if (!directivesAllowed)
            {
                return false;
            }

            ParseDirective(text, s + 2, e, matches);
            return true;
        }

        return false;
    }

    private static int FindInLine(string text, int start, int end, string value)
        => text.IndexOf(value, start, end - start, StringComparison.Ordinal);

    private static bool HasAt(string text, int index, string value)
        => text.AsSpan(index).StartsWith(value, StringComparison.Ordinal);

    // Parses one "#:" directive into a match, mirroring the SDK's parser (see the class remarks). The kind
    // runs from just after "#:" to the first non-letter; unmanaged and malformed directives add nothing.
    private static void ParseDirective(string text, int start, int end, List<DirectiveMatch> matches)
    {
        var k = start;
        while (k < end && char.IsAsciiLetter(text[k]))
        {
            k++;
        }

        AppDirectiveKind? kind = text[start..k] switch
        {
            "package" => AppDirectiveKind.Package,
            "sdk" => AppDirectiveKind.Sdk,
            _ => null,
        };
        if (kind is null)
        {
            return;
        }

        if (k >= end || !char.IsWhiteSpace(text[k]))
        {
            return;
        }

        var v = k;
        while (v < end && char.IsWhiteSpace(text[v]))
        {
            v++;
        }

        if (v >= end)
        {
            return;
        }

        var at = text.IndexOf('@', v, end - v);
        if (at < 0)
        {
            matches.Add(new DirectiveMatch(new AppDirective(kind.Value, text[v..end], null), -1));
            return;
        }

        var idEnd = at;
        while (idEnd > v && char.IsWhiteSpace(text[idEnd - 1]))
        {
            idEnd--;
        }

        if (idEnd == v)
        {
            return;
        }

        var versionStart = at + 1;
        while (versionStart < end && char.IsWhiteSpace(text[versionStart]))
        {
            versionStart++;
        }

        var directive = new AppDirective(kind.Value, text[v..idEnd], text[versionStart..end]);
        matches.Add(new DirectiveMatch(directive, versionStart));
    }
}
