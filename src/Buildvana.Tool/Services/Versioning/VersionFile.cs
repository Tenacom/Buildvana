// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Buildvana.Tool.Utilities;
using Cake.Core;
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

    private readonly ICakeContext _context;

    private VersionFile(ICakeContext context, FilePath path, VersionSpec versionSpec, string firstUnstableTag)
    {
        _context = context;
        Path = path.MakeAbsolute(_context.Environment);
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
    /// <param name="context">The Cake context.</param>
    /// <returns>A newly-created <see cref="VersionFile"/>, representing the loaded data.</returns>
    public static VersionFile Load(ICakeContext context)
    {
        Guard.IsNotNull(context);

        var path = new FilePath(VersionJsonPath);
        var json = context.LoadJsonObject(path);
        var versionStr = context.GetJsonPropertyValue<string>(json, VersionPropertyName, path + " file");
        context.Ensure(VersionSpec.TryParse(versionStr, out var versionSpec), $"{VersionJsonPath} contains invalid version specification '{versionStr}'.");
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

        return new(context, path, versionSpec, firstUnstableTag);
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
        _ = _context.RewriteJsonStringValues(
            Path,
            (propertyPath, _) => propertyPath.Count == 1 && propertyPath[0] == VersionPropertyName ? newVersion : null);
    }
}
