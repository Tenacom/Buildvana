// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the pins of the <c>tools</c> scope: the .NET local tools of the repository's tool manifest.
/// </summary>
/// <remarks>
/// <para>Only the repository's own manifest is read, never an ancestor's, which is the rule the rest of
/// <c>bv</c> follows; see <see cref="ToolManifest"/>. An absent manifest is no pin, not a problem.</para>
/// <para>The <c>bv</c> entry is a family pin and is not among the results: <c>bv self-update</c> is the one
/// command that moves it.</para>
/// </remarks>
internal sealed class ToolPinReader(IHomeDirectoryProvider home, IJsonHelper jsonHelper)
{
    private const string ToolsSectionName = "tools";
    private const string VersionMemberName = "version";

    /// <summary>
    /// Reads the tool manifest's pins.
    /// </summary>
    /// <returns>One pin per tool the manifest states, in the order it states them.</returns>
    /// <exception cref="BuildFailedException">The manifest exists and could not be read or parsed.</exception>
    public IReadOnlyList<DependencyPin> Read()
    {
        var path = home.GetFullPath(ToolManifest.RelativePath);
        if (!File.Exists(path))
        {
            return [];
        }

        if (jsonHelper.LoadObject(path)[ToolsSectionName] is not JsonObject tools)
        {
            return [];
        }

        var pins = new List<DependencyPin>();
        foreach (var (id, node) in tools)
        {
            if (BuildvanaFamily.Contains(id))
            {
                continue;
            }

            // A manifest entry is an object, and its version is a string. What the dotnet CLI makes of an
            // entry shaped otherwise is the CLI's business: bv reads no pin there and moves on.
            if (node is JsonObject entry
                && entry[VersionMemberName] is JsonValue value
                && value.TryGetValue<string>(out var version))
            {
                pins.Add(DependencyPin.Create(DependencyScope.Tools, id, version, ToolManifest.RelativePath));
            }
        }

        return pins;
    }
}
