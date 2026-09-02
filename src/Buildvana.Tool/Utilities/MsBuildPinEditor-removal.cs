// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

partial class MsBuildPinEditor
{
    /// <summary>
    /// Removes pins of the given item types from a file, in place.
    /// </summary>
    /// <param name="path">The path of the file to edit.</param>
    /// <param name="itemTypes">The item element names to look for (e.g. <c>PackageVersion</c>).</param>
    /// <param name="shouldRemove">Called once per pin, in document order; returns <see langword="true"/> for
    /// a pin to remove and <see langword="false"/> for one to leave alone.</param>
    /// <returns><see langword="true"/> if the file was modified; otherwise, <see langword="false"/>.
    /// The file is written only when it was modified.</returns>
    /// <remarks>
    /// <para>An element alone on its line takes the whole line with it, indentation and line ending included.
    /// One that shares its line is cut out on its own, because what stands beside it is not this editor's to
    /// move. Nothing else is reformatted: an item group left with no items stays where it is, empty.</para>
    /// </remarks>
    public static bool RemovePins(
        string path,
        IReadOnlyCollection<string> itemTypes,
        Func<MsBuildPin, bool> shouldRemove)
    {
        Guard.IsNotNull(path);
        Guard.IsNotNull(itemTypes);
        Guard.IsNotNull(shouldRemove);
        var (text, encoding) = TextSpliceHelper.ReadAllTextWithEncoding(path);
        var edits = new List<TextEdit>();
        foreach (var match in Scan(text, itemTypes))
        {
            // An element whose end tag the file never states is one this editor leaves alone: where such an
            // element ends is a guess, and a wrong guess deletes whatever follows it.
            if (match.ElementEnd >= 0 && shouldRemove(match.Pin))
            {
                edits.Add(RemovalOf(text, match));
            }
        }

        if (edits.Count == 0)
        {
            return false;
        }

        UserFile.WriteAllText(path, TextSpliceHelper.ApplyEdits(text, edits), encoding);
        return true;
    }

    // What one removal cuts out: the whole line when the element has the line to itself, and the element
    // alone when anything else shares it.
    private static TextEdit RemovalOf(string text, PinMatch match)
    {
        var lineStart = StartOfLine(text, match.ElementStart);
        var lineEnd = EndOfLine(text, match.ElementEnd);
        var isAlone = IsWhiteSpace(text, lineStart, match.ElementStart) && IsWhiteSpace(text, match.ElementEnd, lineEnd);
        return isAlone
            ? new TextEdit(lineStart, lineEnd - lineStart, string.Empty)
            : new TextEdit(match.ElementStart, match.ElementEnd - match.ElementStart, string.Empty);
    }

    private static int StartOfLine(string text, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            if (text[i] == '\n')
            {
                return i + 1;
            }
        }

        return 0;
    }

    // The index just past the line ending that closes the line, or the end of the text where the last line
    // ends without one.
    private static int EndOfLine(string text, int index)
    {
        var lineEnd = text.IndexOf('\n', index);
        return lineEnd < 0 ? text.Length : lineEnd + 1;
    }

    private static bool IsWhiteSpace(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }
}
