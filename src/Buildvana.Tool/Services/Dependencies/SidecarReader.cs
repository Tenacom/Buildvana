// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Diagnostics;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Utilities;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the transitive overrides in effect, straight from the files that state them.
/// </summary>
/// <remarks>
/// <para>Reading needs no network and no restore, which is what lets <c>bv dependencies show</c> report the
/// overrides offline. What it reports is the state the last apply run left, and no claim about what the next
/// one would write.</para>
/// <para>These files are bv's own and are never edited in place, so they are read as XML. The rule against
/// round-tripping a file through an XML writer is about the files a repository owns, whose formatting must
/// survive an edit; these are rewritten whole every time.</para>
/// </remarks>
internal sealed class SidecarReader(IHomeDirectoryProvider home, IReporter reporter)
{
    private const string CentralItemType = "PackageVersion";
    private const string ProjectItemType = "PackageReference";

    /// <summary>
    /// Reads every override file of the repository.
    /// </summary>
    /// <returns>The entries, by file and then by package id.</returns>
    /// <exception cref="BuildFailedException">The repository could not be walked.</exception>
    public IReadOnlyList<TransitiveOverrideEntry> Read()
    {
        var entries = new List<TransitiveOverrideEntry>();
        foreach (var path in RepositoryFiles.CreateFinder(home).GetFiles())
        {
            var isCentral = string.Equals(path, TransitiveOverrides.CentralFileName, StringComparison.OrdinalIgnoreCase);
            var isProject = path.EndsWith(TransitiveOverrides.ProjectFileSuffix, StringComparison.OrdinalIgnoreCase);
            if (isCentral || isProject)
            {
                ReadFile(entries, path, isCentral ? CentralItemType : ProjectItemType);
            }
        }

        return entries;
    }

    // bv writes both values as attributes, and bv is the only writer these files have.
    private static IEnumerable<TransitiveOverrideEntry> EntriesOf(
        XDocument document,
        string relativePath,
        string itemType)
    {
        foreach (var element in document.Descendants())
        {
            var isItem = string.Equals(element.Name.LocalName, itemType, StringComparison.Ordinal);
            if (isItem && (string?)element.Attribute("Include") is { Length: > 0 } id)
            {
                yield return new TransitiveOverrideEntry(id, (string?)element.Attribute("Version"), relativePath);
            }
        }
    }

    private void ReadFile(List<TransitiveOverrideEntry> entries, string relativePath, string itemType)
    {
        var document = Load(relativePath);
        if (document is null)
        {
            return;
        }

        var fileEntries = EntriesOf(document, relativePath, itemType)
            .OrderBy(static entry => entry.PackageId, StringComparer.OrdinalIgnoreCase);

        entries.AddRange(fileEntries);
    }

    // A file bv wrote and something else mangled is not worth a failed command: the next apply run rewrites
    // it whole, and a report missing one file is better than no report at all.
    private XDocument? Load(string relativePath)
    {
        try
        {
            return XDocument.Load(home.GetFullPath(relativePath));
        }
        catch (Exception exception) when (exception is XmlException || exception.IsIORelatedException)
        {
            reporter.Warning($"{relativePath} could not be read, so the overrides it states are left out of this report.");
            return null;
        }
    }
}
