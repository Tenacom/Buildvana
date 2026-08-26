// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Reads and rewrites the <c>#:package</c> and <c>#:sdk</c> directives of a file-based app by splicing raw
/// text.
/// </summary>
/// <remarks>
/// <para>Directives are read from the file's leading directive block: the lines before the first line that
/// is neither blank, nor a comment, nor a <c>#:</c> directive. A shebang line also belongs to the block —
/// like Roslyn, this editor recognizes <c>#!</c> at the very start of the file only.</para>
/// <para>The directive format mirrors the SDK's own parser (<c>FileLevelDirectiveHelpers</c> in dotnet/sdk):
/// the directive kind is matched case-sensitively; the value splits at its first <c>@</c>, with the id
/// trimmed at its end and the version at its start. Kinds other than <c>package</c> and <c>sdk</c> are not
/// managed and are not read. Malformed directives are ignored, not diagnosed: judging them is the SDK's
/// business at the app's own build time.</para>
/// <para>A rewrite splices only the version text after the <c>@</c>. Everything else — the directive's own
/// spelling included — survives byte for byte, encoding and byte order mark included.</para>
/// </remarks>
internal static partial class AppDirectiveEditor
{
    /// <summary>
    /// Reads the managed directives from a file-based app's leading directive block.
    /// </summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>The <c>#:package</c> and <c>#:sdk</c> directives found, in document order, versionless ones
    /// included.</returns>
    public static IReadOnlyList<AppDirective> ReadDirectives(string path)
    {
        Guard.IsNotNull(path);
        var (text, _) = TextSpliceHelper.ReadAllTextWithEncoding(path);
        return [.. Scan(text).Select(m => m.Directive)];
    }

    /// <summary>
    /// Rewrites the versions of the managed directives in a file-based app's leading directive block, in
    /// place.
    /// </summary>
    /// <param name="path">The path of the file to edit.</param>
    /// <param name="getNewVersionText">Called once per directive that carries a version, in document order;
    /// returns the new version text for the directive, or <see langword="null"/> to leave it alone. Text
    /// ordinally equal to the current version leaves the directive alone too.</param>
    /// <returns><see langword="true"/> if the file was modified; otherwise, <see langword="false"/>.
    /// The file is written only when it was modified.</returns>
    public static bool RewriteVersions(string path, Func<AppDirective, string?> getNewVersionText)
    {
        Guard.IsNotNull(path);
        Guard.IsNotNull(getNewVersionText);
        var (text, encoding) = TextSpliceHelper.ReadAllTextWithEncoding(path);
        var edits = new List<TextEdit>();
        foreach (var match in Scan(text))
        {
            if (match.Directive.VersionText is not { } versionText)
            {
                continue;
            }

            var newVersionText = getNewVersionText(match.Directive);
            if (newVersionText is not null && !string.Equals(newVersionText, versionText, StringComparison.Ordinal))
            {
                edits.Add(new TextEdit(match.VersionStart, versionText.Length, newVersionText));
            }
        }

        if (edits.Count == 0)
        {
            return false;
        }

        UserFile.WriteAllText(path, TextSpliceHelper.ApplyEdits(text, edits), encoding);
        return true;
    }
}
