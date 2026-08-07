// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Buildvana.Core.Json;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// Reads bv's own entry in a repository's .NET tool manifest (<c>.config/dotnet-tools.json</c>).
/// </summary>
internal static class ToolManifest
{
    /// <summary>
    /// The path of the tool manifest, relative to the home directory.
    /// </summary>
    public const string RelativePath = ".config/dotnet-tools.json";

    /// <summary>
    /// The ID of bv's NuGet package, which is also its tool command name.
    /// </summary>
    public const string BvPackageId = "bv";

    /// <summary>
    /// Reads the bv entry in the tool manifest of the given home directory.
    /// </summary>
    /// <remarks>
    /// <para>The bv entry is matched case-insensitively: NuGet package IDs are case-insensitive, and the
    /// dotnet CLI normalizes manifest keys through its <c>PackageId</c> type (which lowercases them), so a
    /// differently-cased entry is fully usable by <c>dotnet tool restore</c> and <c>tool run</c> and must be
    /// found here too.</para>
    /// <para>Only the given home directory's own manifest is read — deliberately narrower than the dotnet CLI,
    /// which walks up from the working directory merging manifests until one is marked <c>isRoot</c>. An
    /// ancestor manifest's bv entry therefore pins bv for the CLI but not for bv itself: the repository's own
    /// manifest is the pin bv manages, and both delegation and <c>bv update</c> scope themselves to it.</para>
    /// <para>Version parseability is judged with <see cref="NuGetVersion.TryParse(string?, out NuGetVersion)"/> — the
    /// same call the dotnet CLI makes when it reads the manifest (see <c>ToolManifestEditor</c> in the
    /// dotnet/sdk repository) — so "no usable version" here is exactly "a manifest the dotnet CLI cannot use".</para>
    /// </remarks>
    /// <param name="jsonHelper">The JSON helper used to read the manifest.</param>
    /// <param name="homeDirectory">The home directory whose manifest to read.</param>
    /// <returns>What the manifest says about bv; a missing manifest reads as no entry.</returns>
    /// <exception cref="Buildvana.Core.BuildFailedException">The manifest exists but cannot be read or parsed.</exception>
    public static BvManifestPin ReadBvPin(IJsonHelper jsonHelper, string homeDirectory)
    {
        Guard.IsNotNull(jsonHelper);
        Guard.IsNotNullOrEmpty(homeDirectory);

        var path = Path.Combine(homeDirectory, RelativePath);
        if (!File.Exists(path))
        {
            return new BvManifestPin(HasEntry: false, VersionText: null, Version: null);
        }

        var manifest = jsonHelper.LoadObject(path);
        var toolNode = manifest["tools"] is JsonObject tools ? FindBvEntry(tools) : null;
        if (toolNode is not JsonObject toolEntry)
        {
            return new BvManifestPin(HasEntry: false, VersionText: null, Version: null);
        }

        string? versionText = null;
        _ = toolEntry["version"] is JsonValue versionValue && versionValue.TryGetValue(out versionText);
        var version = NuGetVersion.TryParse(versionText, out var parsed) ? parsed : null;
        return new BvManifestPin(HasEntry: true, versionText, version);
    }

    private static JsonNode? FindBvEntry(JsonObject tools)
    {
        foreach (var (name, node) in tools)
        {
            if (string.Equals(name, BvPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }
}
