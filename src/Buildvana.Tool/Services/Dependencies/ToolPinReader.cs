// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Utilities;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the pins of the <c>tools</c> scope: the .NET local tools of every tool manifest under the home
/// directory.
/// </summary>
/// <remarks>
/// <para>Every <c>dotnet-tools.json</c> the repository owns is read, the home directory's and the ones in
/// subdirectories alike, the way the <c>packages</c> scope reads every project file: a pin belongs to the
/// file that declares it, and <see cref="ToolPinUpdater"/> writes it back there. An ancestor's manifest is
/// never read, which is the rule the rest of <c>bv</c> follows; see <see cref="ToolManifest"/>. An absent
/// manifest is no pin, not a problem. A manifest under <c>.config</c> fails the read, at any depth.</para>
/// <para>The <c>bv</c> entry of the home directory's manifest is a family pin and is not among the results: it
/// is the entry that delegation runs and <c>bv self-update</c> moves. A <c>bv</c> entry in any other manifest is
/// read like any other tool, because delegation and <c>bv self-update</c> read the home directory's manifest
/// alone.</para>
/// </remarks>
internal sealed class ToolPinReader(IHomeDirectoryProvider home, IJsonHelper jsonHelper)
{
    private const string ToolsSectionName = "tools";
    private const string VersionMemberName = "version";

    /// <summary>
    /// Reads the pins of every tool manifest under the home directory.
    /// </summary>
    /// <returns>One pin per tool the manifests state, manifest by manifest in the order the repository walk
    /// finds them, and within a manifest in the order it states them.</returns>
    /// <exception cref="BuildFailedException">A manifest could not be read or parsed, or a manifest sits under
    /// <c>.config</c>. In the latter case the message names every such manifest and the move that fixes
    /// it.</exception>
    public IReadOnlyList<DependencyPin> Read()
    {
        var manifestPaths = new List<string>();
        var legacyPaths = new List<string>();
        foreach (var relativePath in RepositoryFiles.CreateFinder(home).GetFiles())
        {
            if (ToolManifest.IsLegacyManifestPath(relativePath))
            {
                legacyPaths.Add(relativePath);
            }
            else if (ToolManifest.IsManifestPath(relativePath))
            {
                manifestPaths.Add(relativePath);
            }
        }

        // The manifests under .config are reported before any manifest is parsed: a repository with both
        // problems would otherwise hear about the parse error alone.
        if (legacyPaths.Count > 0)
        {
            throw ToolManifest.LegacyManifestError(home.HomeDirectory, legacyPaths);
        }

        var pins = new List<DependencyPin>();
        foreach (var relativePath in manifestPaths)
        {
            ReadManifest(relativePath, pins);
        }

        return pins;
    }

    private void ReadManifest(string relativePath, List<DependencyPin> pins)
    {
        if (jsonHelper.LoadObject(home.GetFullPath(relativePath))[ToolsSectionName] is not JsonObject tools)
        {
            return;
        }

        var isHomeManifest = relativePath == ToolManifest.FileName;
        foreach (var (id, node) in tools)
        {
            if (isHomeManifest && BuildvanaFamily.Contains(id))
            {
                continue;
            }

            // A manifest entry is an object, and its version is a string. What the dotnet CLI makes of an
            // entry shaped otherwise is the CLI's business: bv reads no pin there and moves on.
            if (node is JsonObject entry
                && entry[VersionMemberName] is JsonValue value
                && value.TryGetValue<string>(out var version))
            {
                pins.Add(DependencyPin.Create(DependencyScope.Tools, id, version, relativePath));
            }
        }
    }
}
