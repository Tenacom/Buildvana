// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.Json;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// Locates a repository's .NET tool manifest (<c>dotnet-tools.json</c>) and reads bv's own entry in it.
/// </summary>
/// <remarks>
/// <para>The .NET SDK before version 10 created the manifest under a <c>.config</c> subdirectory, and the
/// dotnet CLI still reads and writes it there. bv does not: it reads <c>dotnet-tools.json</c> in the home
/// directory, and fails on a manifest under <c>.config</c> instead of ignoring it, because the CLI would keep
/// writing bv's own pin into a file bv never reads. The failure names the move.</para>
/// </remarks>
internal static class ToolManifest
{
    /// <summary>
    /// The file name of a tool manifest. The home directory's manifest is this name, relative to the home
    /// directory.
    /// </summary>
    public const string FileName = "dotnet-tools.json";

    /// <summary>
    /// The path of the tool manifest bv rejects, relative to the home directory: the one the .NET SDK before
    /// version 10 created.
    /// </summary>
    public const string LegacyRelativePath = ".config/" + FileName;

    /// <summary>
    /// The ID of bv's NuGet package, which is also its tool command name.
    /// </summary>
    public const string BvPackageId = BuildvanaFamily.ToolPackageId;

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
    /// manifest is the pin bv manages, and both delegation and <c>bv self-update</c> key their decisions on it
    /// alone. The dotnet CLI they spawn is pointed at this same manifest with <c>--tool-manifest</c>, so its
    /// own upward walk from the home directory plays no part.</para>
    /// <para>A manifest under <c>.config</c> fails the read; see the class remarks.</para>
    /// <para>Version parseability is judged with <see cref="NuGetVersion.TryParse(string?, out NuGetVersion)"/> — the
    /// same call the dotnet CLI makes when it reads the manifest (see <c>ToolManifestEditor</c> in the
    /// dotnet/sdk repository) — so "no usable version" here is exactly "a manifest the dotnet CLI cannot use".</para>
    /// </remarks>
    /// <param name="jsonHelper">The JSON helper used to read the manifest.</param>
    /// <param name="homeDirectory">The home directory whose manifest to read.</param>
    /// <returns>What the manifest says about bv; a missing manifest reads as no entry.</returns>
    /// <exception cref="BuildFailedException">The manifest exists but cannot be read or parsed, or a manifest sits
    /// under <c>.config</c>.</exception>
    public static BvManifestPin ReadBvPin(IJsonHelper jsonHelper, string homeDirectory)
    {
        Guard.IsNotNull(jsonHelper);
        Guard.IsNotNullOrEmpty(homeDirectory);

        EnsureNoLegacyManifest(homeDirectory);
        var path = Path.Combine(homeDirectory, FileName);
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

    /// <summary>
    /// Fails when the home directory holds a tool manifest under <c>.config</c>.
    /// </summary>
    /// <param name="homeDirectory">The home directory to check.</param>
    /// <exception cref="BuildFailedException"><see cref="LegacyRelativePath"/> exists. The message names the
    /// fix.</exception>
    public static void EnsureNoLegacyManifest(string homeDirectory)
    {
        Guard.IsNotNullOrEmpty(homeDirectory);
        if (File.Exists(Path.Combine(homeDirectory, LegacyRelativePath)))
        {
            throw LegacyManifestError(homeDirectory, [LegacyRelativePath]);
        }
    }

    /// <summary>
    /// Tells whether a path names a tool manifest, at any depth.
    /// </summary>
    /// <param name="relativePath">The path, relative to the home directory, with <c>/</c> as the separator.</param>
    /// <returns><see langword="true"/> when the file is named <c>dotnet-tools.json</c>.</returns>
    public static bool IsManifestPath(string relativePath)
    {
        Guard.IsNotNull(relativePath);
        return relativePath == FileName || relativePath.EndsWith("/" + FileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tells whether a path names a tool manifest under a <c>.config</c> directory, at any depth.
    /// </summary>
    /// <param name="relativePath">The path, relative to the home directory, with <c>/</c> as the separator.</param>
    /// <returns><see langword="true"/> when the path ends with <see cref="LegacyRelativePath"/>.</returns>
    public static bool IsLegacyManifestPath(string relativePath)
    {
        Guard.IsNotNull(relativePath);
        return relativePath == LegacyRelativePath
            || relativePath.EndsWith("/" + LegacyRelativePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates the failure that reports tool manifests under <c>.config</c>.
    /// </summary>
    /// <remarks>
    /// <para>The fix for a manifest is the <c>git mv</c> that moves it up one level. When a manifest exists at
    /// the destination as well, the dotnet CLI reads the two as one manifest and writes into the one under
    /// <c>.config</c>, and a move would fail on the existing file. The fix is then a merge into the manifest bv
    /// reads.</para>
    /// </remarks>
    /// <param name="homeDirectory">The home directory the paths are relative to.</param>
    /// <param name="relativePaths">The paths of the manifests, relative to the home directory, with <c>/</c> as
    /// the separator.</param>
    /// <returns>The failure. Its message names each file and its fix.</returns>
    public static BuildFailedException LegacyManifestError(string homeDirectory, IReadOnlyList<string> relativePaths)
    {
        Guard.IsNotNullOrEmpty(homeDirectory);
        Guard.IsNotEmpty(relativePaths);
        if (relativePaths.Count > 1)
        {
            var fixes = string.Join("; ", relativePaths.Select(path => FixFor(homeDirectory, path)));
            return new BuildFailedException(
                $"Tool manifests are at {string.Join(", ", relativePaths)}, where bv does not read them. Fix each: {fixes}");
        }

        var legacyPath = relativePaths[0];
        var movedPath = MovedPathOf(legacyPath);
        return ExistsIn(homeDirectory, movedPath)
            ? new BuildFailedException(
                $"A tool manifest is at {legacyPath}, where bv does not read it, and another is at {movedPath}. "
                + $"Merge the tools of {legacyPath} into {movedPath}, then delete {legacyPath}.")
            : new BuildFailedException(
                $"The tool manifest is at {legacyPath}, where bv does not read it. "
                + $"Move it up one level: git mv {legacyPath} {movedPath}");
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

    // The fix for one manifest, as a phrase of a list.
    private static string FixFor(string homeDirectory, string legacyPath)
    {
        var movedPath = MovedPathOf(legacyPath);
        return ExistsIn(homeDirectory, movedPath)
            ? $"merge the tools of {legacyPath} into {movedPath}, then delete {legacyPath}"
            : $"git mv {legacyPath} {movedPath}";
    }

    // ".config/dotnet-tools.json" becomes "dotnet-tools.json", and "docs/.config/dotnet-tools.json" becomes
    // "docs/dotnet-tools.json".
    private static string MovedPathOf(string legacyPath)
        => legacyPath[..^LegacyRelativePath.Length] + FileName;

    private static bool ExistsIn(string homeDirectory, string relativePath)
        => File.Exists(Path.Combine(homeDirectory, relativePath));
}
