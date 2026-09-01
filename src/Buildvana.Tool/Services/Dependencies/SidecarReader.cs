// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Diagnostics;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

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

    private static IEnumerable<XElement> ItemsOf(XDocument document, string itemType)
        => document.Descendants().Where(element => string.Equals(element.Name.LocalName, itemType, StringComparison.Ordinal));

    private void ReadFile(List<TransitiveOverrideEntry> entries, string relativePath, string itemType)
    {
        var document = Load(relativePath);
        if (document is null)
        {
            return;
        }

        // bv writes both values as attributes, and bv is the only writer these files have.
        var items = ItemsOf(document, itemType)
            .Select(static element => (Id: (string?)element.Attribute("Include"), Version: (string?)element.Attribute("Version")))
            .Where(static item => !string.IsNullOrEmpty(item.Id))
            .OrderBy(static item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var (id, version) in items)
        {
            entries.Add(new TransitiveOverrideEntry(id!, version, relativePath));
        }
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
