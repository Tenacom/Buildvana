// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Utilities;

/// <summary>
/// Reads and rewrites package pins in MSBuild-syntax files (<c>Directory.Packages.props</c>, project files,
/// shared props/targets files) by splicing raw text, never by round-tripping through an XML writer.
/// </summary>
/// <remarks>
/// <para>A pin is an item element of a caller-named type that carries an <c>Include</c> attribute and a
/// <c>Version</c> value, the latter as an attribute or as a child element (the attribute wins when both are
/// present). Item elements are located anywhere in the file, by element name, case-insensitively — matching
/// MSBuild's own insensitivity to item-type casing — with any attribute order and either quoting style.
/// Comments, CDATA sections, and processing instructions are skipped, so a commented-out item is never read
/// or edited. Items without an <c>Include</c> or without a <c>Version</c> are not pins and are not seen; in
/// particular, <c>Update</c>-form items are invisible to this editor by design — whoever manages references
/// through <c>Update</c> items manages their versions too.</para>
/// <para>The editor never evaluates: conditions are ignored, and properties are not expanded. A pin whose
/// version is written as <c>$(SomeProperty)</c> reaches the caller verbatim; leaving the indirection alone
/// is the caller's decision. Two declarations of one id — e.g. conditioned per target framework — are two
/// pins, each presented on its own, in document order.</para>
/// <para>A rewrite splices only the version value. Formatting, comments, attribute order, quoting, line
/// endings, and encoding (byte order mark included) all survive byte for byte. An XML writer would reformat:
/// <c>dotnet package</c> compacts imported files for exactly that reason, which is why this editor never
/// loads the document as XML.</para>
/// </remarks>
internal static partial class MsBuildPinEditor
{
    /// <summary>
    /// Reads the pins of the given item types from a file.
    /// </summary>
    /// <param name="path">The path of the file to read.</param>
    /// <param name="itemTypes">The item element names to look for (e.g. <c>PackageVersion</c>).</param>
    /// <returns>The pins found, in document order.</returns>
    public static IReadOnlyList<MsBuildPin> ReadPins(string path, IReadOnlyCollection<string> itemTypes)
    {
        Guard.IsNotNull(path);
        Guard.IsNotNull(itemTypes);
        var (text, _) = TextSpliceHelper.ReadAllTextWithEncoding(path);
        return [.. Scan(text, itemTypes).Select(m => m.Pin)];
    }

    /// <summary>
    /// Rewrites the versions of the pins of the given item types in a file, in place.
    /// </summary>
    /// <param name="path">The path of the file to edit.</param>
    /// <param name="itemTypes">The item element names to look for (e.g. <c>PackageVersion</c>).</param>
    /// <param name="getNewVersionText">Called once per pin, in document order; returns the new version text
    /// for the pin, or <see langword="null"/> to leave it alone. Text ordinally equal to the current version
    /// leaves the pin alone too.</param>
    /// <returns><see langword="true"/> if the file was modified; otherwise, <see langword="false"/>.
    /// The file is written only when it was modified.</returns>
    public static bool RewritePins(
        string path,
        IReadOnlyCollection<string> itemTypes,
        Func<MsBuildPin, string?> getNewVersionText)
    {
        Guard.IsNotNull(path);
        Guard.IsNotNull(itemTypes);
        Guard.IsNotNull(getNewVersionText);
        var (text, encoding) = TextSpliceHelper.ReadAllTextWithEncoding(path);
        var edits = new List<TextEdit>();
        foreach (var match in Scan(text, itemTypes))
        {
            var newVersionText = getNewVersionText(match.Pin);
            if (newVersionText is not null && !string.Equals(newVersionText, match.Pin.VersionText, StringComparison.Ordinal))
            {
                edits.Add(new TextEdit(match.VersionStart, match.Pin.VersionText.Length, newVersionText));
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
