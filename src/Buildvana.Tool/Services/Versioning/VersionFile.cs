// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Versioning;

/// <summary>
/// Represents the <c>version.json</c> file, for the purpose of applying version advances.
/// </summary>
public sealed class VersionFile
{
    private const string VersionJsonPath = "version.json";
    private const string VersionPropertyName = "version";
    private const string DefaultFirstUnstableTag = "preview";

    private readonly IJsonHelper _jsonHelper;

    private VersionFile(
        IJsonHelper jsonHelper,
        FilePath absolutePath,
        VersionSpec versionSpec,
        string firstUnstableTag)
    {
        _jsonHelper = jsonHelper;
        Path = absolutePath;
        VersionSpec = versionSpec;
        FirstUnstableTag = firstUnstableTag;
    }

    /// <summary>
    /// Gets the <see cref="FilePath"/> of the <c>version.json</c> file.
    /// </summary>
    public FilePath Path { get; }

    /// <summary>
    /// Gets a <see cref="VersionSpec"/> representing the "version" value in the <c>version.json</c> file.
    /// </summary>
    public VersionSpec VersionSpec { get; private set; }

    /// <summary>
    /// Gets the unstable tag to use for version advances.
    /// </summary>
    /// <value>Either the "release.firstUnstableTag" value read from version.json, or "preview" as a default value.</value>
    public string FirstUnstableTag { get; }

    /// <summary>
    /// Constructs a <see cref="VersionFile"/> instance by loading the repository's <c>version.json</c> file.
    /// </summary>
    /// <param name="home">The home directory provider used to resolve the path of <c>version.json</c>.</param>
    /// <param name="jsonHelper">The JSON helper used to load and rewrite the file.</param>
    /// <returns>A newly-created <see cref="VersionFile"/>, representing the loaded data.</returns>
    public static VersionFile Load(IHomeDirectoryProvider home, IJsonHelper jsonHelper)
    {
        Guard.IsNotNull(home);
        Guard.IsNotNull(jsonHelper);

        var path = new FilePath(VersionJsonPath).MakeAbsolute(new DirectoryPath(home.HomeDirectory));
        var json = jsonHelper.LoadObject(path.FullPath);
        var versionStr = jsonHelper.GetPropertyValue<string>(json, VersionPropertyName, path + " file");
        BuildFailedException.ThrowIfNot(VersionSpec.TryParse(versionStr, out var versionSpec), $"{VersionJsonPath} contains invalid version specification '{versionStr}'.");
        var firstUnstableTag = DefaultFirstUnstableTag;
        var release = json["release"];
        if (release is not null)
        {
            var firstUnstableTagNode = release["firstUnstableTag"];
            if (firstUnstableTagNode is JsonValue firstUnstableTagValue && firstUnstableTagValue.TryGetValue<string>(out var firstUnstableTagStr) && !string.IsNullOrEmpty(firstUnstableTagStr))
            {
                firstUnstableTag = firstUnstableTagStr;
            }
        }

        return new(jsonHelper, path, versionSpec, firstUnstableTag);
    }

    /// <summary>
    /// Applies a version spec change to this instance.
    /// </summary>
    /// <param name="change">A <see cref="VersionSpecChange"/> constant representing the kind of change to apply.</param>
    /// <returns>If the <see cref="VersionSpec"/> property is actually changed as a result of <paramref name="change"/>, <see langword="true"/>;
    /// otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>This method does not save the modified <c>version.json</c> file; you will have to call
    /// the <see cref="Save"/> method if this method returns <see langword="true"/>.</para>
    /// </remarks>
    public bool ApplyVersionSpecChange(VersionSpecChange change)
    {
        (VersionSpec, var changed) = VersionSpec.ApplyChange(change, FirstUnstableTag);
        return changed;
    }

    /// <summary>
    /// Saves the <c>version.json</c> file, possibly with a modified <see cref="VersionSpec"/>, back to the repository.
    /// </summary>
    public void Save()
    {
        var newVersion = VersionSpec.ToString();
        var rewritten = _jsonHelper.RewriteStringValues(
            Path.FullPath,
            (propertyPath, _) => propertyPath is [VersionPropertyName] ? newVersion : null);

        // Load already validated that a top-level string "version" property exists, so a no-op here
        // means either the on-disk file changed underneath us or VersionSpec.ToString() produced the
        // same string the file already held — both cases would let the release flow stage stale data.
        BuildFailedException.ThrowIfNot(rewritten, $"Could not update {VersionJsonPath}: expected a top-level string '{VersionPropertyName}' property to rewrite.");
    }
}
