// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// Reads the bv version pinned in the tool manifest of the given home directory.
    /// </summary>
    /// <param name="jsonHelper">The JSON helper used to read the manifest.</param>
    /// <param name="homeDirectory">The home directory whose manifest to read.</param>
    /// <returns>The pinned version, or <see langword="null"/> when the manifest, the bv entry, or a parseable
    /// version is missing.</returns>
    /// <exception cref="Buildvana.Core.BuildFailedException">The manifest exists but cannot be read or parsed.</exception>
    public static NuGetVersion? ReadBvPin(IJsonHelper jsonHelper, string homeDirectory)
    {
        Guard.IsNotNull(jsonHelper);
        Guard.IsNotNullOrEmpty(homeDirectory);

        var path = Path.Combine(homeDirectory, RelativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        var manifest = jsonHelper.LoadObject(path);
        string? version = null;
        var hasEntry = manifest.TryGetPropertyValue("tools", out var toolsNode)
            && toolsNode is JsonObject tools
            && tools.TryGetPropertyValue(BvPackageId, out var toolNode)
            && toolNode is JsonObject toolEntry
            && toolEntry.TryGetPropertyValue("version", out var versionNode)
            && versionNode is JsonValue versionValue
            && versionValue.TryGetValue(out version);
        return hasEntry && NuGetVersion.TryParse(version, out var parsed) ? parsed : null;
    }
}
