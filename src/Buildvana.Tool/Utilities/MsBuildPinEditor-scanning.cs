// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Tool.Utilities;

partial class MsBuildPinEditor
{
    // Scans raw MSBuild-syntax text for item elements of the wanted types that carry an Include attribute
    // and a Version value. Tolerant by design: malformed content never throws — what cannot be parsed is
    // skipped, resynchronizing at the next '<'.
    private static List<PinMatch> Scan(string text, IReadOnlyCollection<string> itemTypes)
    {
        var wanted = new HashSet<string>(itemTypes, StringComparer.OrdinalIgnoreCase);
        var matches = new List<PinMatch>();
        var i = 0;
        while (i < text.Length)
        {
            var lt = text.IndexOf('<', i);
            if (lt < 0)
            {
                break;
            }

            if (IsMarkupStart(text, lt))
            {
                i = SkipMarkup(text, lt);
                continue;
            }

            if (!TryParseStartTag(text, lt, out var tag))
            {
                i = lt + 1;
                continue;
            }

            i = tag.End;
            if (!wanted.Contains(tag.Name))
            {
                continue;
            }

            if (tag.IncludeValue is not { Length: > 0 } id)
            {
                continue;
            }

            if (tag.VersionStart >= 0)
            {
                var versionText = text.Substring(tag.VersionStart, tag.VersionLength);
                matches.Add(new PinMatch(new MsBuildPin(tag.Name, id, versionText), tag.VersionStart));
                continue;
            }

            if (tag.SelfClosing)
            {
                continue;
            }

            if (FindVersionChild(text, tag.End, out var continueAt) is { } child)
            {
                var versionText = text.Substring(child.Start, child.Length);
                matches.Add(new PinMatch(new MsBuildPin(tag.Name, id, versionText), child.Start));
            }

            i = continueAt;
        }

        return matches;
    }

    // Whether the '<' at the given index introduces anything but a start tag: a comment, a CDATA section,
    // a processing instruction, a doctype, or an end tag.
    private static bool IsMarkupStart(string text, int lt)
        => lt + 1 < text.Length && text[lt + 1] is '!' or '?' or '/';

    private static int SkipMarkup(string text, int lt)
    {
        if (HasAt(text, lt, "<!--"))
        {
            return SkipTo(text, lt + 4, "-->");
        }

        if (HasAt(text, lt, "<![CDATA["))
        {
            return SkipTo(text, lt + 9, "]]>");
        }

        if (HasAt(text, lt, "<?"))
        {
            return SkipTo(text, lt + 2, "?>");
        }

        // An end tag, a doctype, or any other "<!" construct. No '>' can hide inside these in an MSBuild
        // file: attribute values (the one place XML allows a raw '>') occur in start tags only.
        return SkipTo(text, lt + 1, ">");
    }

    // Parses a start tag at the given '<'. Returns false on malformed content, leaving the caller to
    // resynchronize. Attributes are parsed in full — never skipped by searching for '>' — because XML
    // allows a raw '>' inside a quoted attribute value, and MSBuild Condition attributes actually use one.
    private static bool TryParseStartTag(string text, int lt, out StartTag tag)
    {
        tag = default;
        var i = lt + 1;
        var nameStart = i;
        while (i < text.Length && IsNameChar(text[i]))
        {
            i++;
        }

        if (i == nameStart)
        {
            return false;
        }

        var name = text[nameStart..i];
        string? includeValue = null;
        var versionStart = -1;
        var versionLength = 0;
        while (true)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                return false;
            }

            var c = text[i];
            if (c == '>')
            {
                tag = new StartTag(name, includeValue, versionStart, versionLength, SelfClosing: false, End: i + 1);
                return true;
            }

            if (c == '/')
            {
                var isTagEnd = i + 1 < text.Length && text[i + 1] == '>';
                if (!isTagEnd)
                {
                    return false;
                }

                tag = new StartTag(name, includeValue, versionStart, versionLength, SelfClosing: true, End: i + 2);
                return true;
            }

            if (!TryParseAttribute(text, ref i, out var attributeName, out var valueStart, out var valueLength))
            {
                return false;
            }

            if (string.Equals(attributeName, "Include", StringComparison.OrdinalIgnoreCase))
            {
                includeValue = text.Substring(valueStart, valueLength);
            }
            else if (string.Equals(attributeName, "Version", StringComparison.OrdinalIgnoreCase))
            {
                versionStart = valueStart;
                versionLength = valueLength;
            }
        }
    }

    // Parses one name="value" attribute starting at i (its first character). Advances i past the closing
    // quote on success. Either quoting style is accepted; the value span is taken raw, entities and all —
    // package ids and version texts never contain any.
    private static bool TryParseAttribute(
        string text,
        ref int i,
        out string name,
        out int valueStart,
        out int valueLength)
    {
        name = string.Empty;
        valueStart = 0;
        valueLength = 0;
        var nameStart = i;
        while (i < text.Length && IsNameChar(text[i]))
        {
            i++;
        }

        if (i == nameStart)
        {
            return false;
        }

        name = text[nameStart..i];
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        if (i >= text.Length || text[i] != '=')
        {
            return false;
        }

        i++;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        if (i >= text.Length || text[i] is not ('"' or '\''))
        {
            return false;
        }

        var quote = text[i];
        i++;
        valueStart = i;
        var closing = text.IndexOf(quote, i);
        if (closing < 0)
        {
            return false;
        }

        valueLength = closing - valueStart;
        i = closing + 1;
        return true;
    }

    // Scans the content of an open item element for a direct <Version> child holding plain text. Returns
    // the text's span, or null when the element closes (or the text ends) without such a child; either
    // way, continueAt is where the outer scan should resume, just past whatever was consumed.
    private static VersionChild? FindVersionChild(string text, int contentStart, out int continueAt)
    {
        var depth = 1;
        var i = contentStart;
        while (i < text.Length)
        {
            var lt = text.IndexOf('<', i);
            if (lt < 0)
            {
                break;
            }

            if (lt + 1 < text.Length && text[lt + 1] == '/')
            {
                i = SkipTo(text, lt + 1, ">");
                depth--;
                if (depth == 0)
                {
                    continueAt = i;
                    return null;
                }

                continue;
            }

            if (IsMarkupStart(text, lt))
            {
                i = SkipMarkup(text, lt);
                continue;
            }

            if (!TryParseStartTag(text, lt, out var tag))
            {
                i = lt + 1;
                continue;
            }

            i = tag.End;
            var isVersionChild = depth == 1
                && !tag.SelfClosing
                && string.Equals(tag.Name, "Version", StringComparison.OrdinalIgnoreCase);
            if (!isVersionChild)
            {
                if (!tag.SelfClosing)
                {
                    depth++;
                }

                continue;
            }

            // The child's content must be plain text up to its own end tag; anything else (a comment,
            // nested markup) makes this Version element unusable, and the scan continues past it.
            var valueEnd = text.IndexOf('<', tag.End);
            if (valueEnd < 0)
            {
                break;
            }

            if (!IsEndTag(text, valueEnd, "Version"))
            {
                depth++;
                continue;
            }

            continueAt = SkipTo(text, valueEnd + 1, ">");
            return new VersionChild(tag.End, valueEnd - tag.End);
        }

        continueAt = text.Length;
        return null;
    }

    // Whether the '<' at the given index opens an end tag for the given name (any casing, optional
    // whitespace before the '>').
    private static bool IsEndTag(string text, int lt, string name)
    {
        var i = lt + 1;
        if (i >= text.Length || text[i] != '/')
        {
            return false;
        }

        i++;
        if (!HasAtIgnoreCase(text, i, name))
        {
            return false;
        }

        i += name.Length;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i < text.Length && text[i] == '>';
    }

    // Skips to just past the next occurrence of the given terminator, or to the end of the text when the
    // terminator never occurs (malformed content; the scan just ends).
    private static int SkipTo(string text, int start, string terminator)
    {
        var index = text.IndexOf(terminator, start, StringComparison.Ordinal);
        return index < 0 ? text.Length : index + terminator.Length;
    }

    private static bool HasAt(string text, int index, string value)
        => text.AsSpan(index).StartsWith(value, StringComparison.Ordinal);

    private static bool HasAtIgnoreCase(string text, int index, string value)
        => text.AsSpan(index).StartsWith(value, StringComparison.OrdinalIgnoreCase);

    private static bool IsNameChar(char c)
        => char.IsLetterOrDigit(c) || c is '_' or '.' or '-' or ':';
}
