// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buildvana.Core.IO;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// File plumbing shared by the splice-editing components (<see cref="MsBuildPinEditor"/> and
/// <see cref="AppDirectiveEditor"/>): reads a file remembering its encoding, and applies
/// <see cref="TextEdit"/>s to a text without touching anything outside the edited ranges.
/// </summary>
internal static class TextSpliceHelper
{
    /// <summary>
    /// Reads a file's whole text, detecting the encoding from a byte order mark when present.
    /// </summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>The text, and the encoding to use when rewriting the file so that the rewrite preserves the
    /// original encoding exactly. The fallback when no byte order mark is present is UTF-8 without one;
    /// using the static <see cref="Encoding.UTF8"/> as fallback (which has <c>emitBOM=true</c>) would
    /// silently add a mark on rewrite to files that did not have one.</returns>
    public static (string Text, Encoding Encoding) ReadAllTextWithEncoding(string path)
    {
        using var reader = UserFile.OpenText(path, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        return (text, reader.CurrentEncoding);
    }

    /// <summary>
    /// Applies a set of non-overlapping edits to a text.
    /// </summary>
    /// <param name="text">The text to edit.</param>
    /// <param name="edits">The edits to apply, in any order.</param>
    /// <returns>The edited text.</returns>
    public static string ApplyEdits(string text, IReadOnlyList<TextEdit> edits)
    {
        // Apply in descending start order, so that an applied edit never shifts the start of a pending one.
        var builder = new StringBuilder(text);
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            _ = builder.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        }

        return builder.ToString();
    }
}
