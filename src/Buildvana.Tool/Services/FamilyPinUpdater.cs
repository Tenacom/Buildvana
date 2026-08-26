// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services;

/// <summary>
/// Finds and stamps the family pins declared in the repository's own files: package items in MSBuild-syntax
/// files (projects, shared props/targets files) and versioned <c>#:package</c>/<c>#:sdk</c> directives in
/// file-based apps. The tool manifest, the <c>global.json</c> pin, and the configuration file's schema
/// reference are <see cref="SelfVersionService"/>'s own targets, not this class's.
/// </summary>
/// <remarks>
/// <para>Discovery is textual, through the splice editors — never MSBuild evaluation, which would need the
/// very SDK a self-update may be about to change. Files come from a gitignore-aware walk of the home
/// directory (see <see cref="FileFinder"/>), with the conventional build-output and dependency directories
/// excluded on top, so build debris never contributes a pin.</para>
/// <para>MSBuild items are read from files whose extension is <c>.props</c>, <c>.targets</c>, or any
/// <c>proj</c>-suffixed form (<c>.csproj</c>, <c>.esproj</c>, ...), for the item types
/// <c>PackageVersion</c>, <c>GlobalPackageReference</c>, and <c>PackageReference</c>; a
/// <c>VersionOverride</c> is never read or stamped. Directives are read from every <c>.cs</c> file; a
/// versionless directive is a reference to a pin declared elsewhere, not a pin, and is not seen.</para>
/// </remarks>
internal sealed class FamilyPinUpdater(IHomeDirectoryProvider home)
{
    // Exclusions on top of what .gitignore files dictate: bv's own outputs, anchored at the home directory,
    // plus the conventional build and dependency directories at any depth. The finder skips `.git` on its own.
    private static readonly string[] ExclusionPatterns =
    [
        "/" + CommonPaths.AllArtifacts + "/",
        "/" + CommonPaths.Scratch + "/",
        "bin/",
        "obj/",
        "node_modules/",
    ];

    private static readonly string[] MsBuildItemTypes = ["PackageVersion", "GlobalPackageReference", "PackageReference"];

    /// <summary>
    /// Walks the home directory and returns every family pin found, in walk order.
    /// </summary>
    /// <returns>The pins found.</returns>
    /// <exception cref="BuildFailedException">A directory or file could not be read.</exception>
    public IReadOnlyList<FamilyPin> DiscoverPins()
    {
        var pins = new List<FamilyPin>();
        foreach (var relativePath in new FileFinder(home.HomeDirectory, ExclusionPatterns).GetFiles())
        {
            var path = home.GetFullPath(relativePath);
            if (IsMsBuildFile(relativePath))
            {
                foreach (var pin in MsBuildPinEditor.ReadPins(path, MsBuildItemTypes))
                {
                    if (BuildvanaFamily.Contains(pin.Id))
                    {
                        pins.Add(CreatePin(relativePath, pin.Id, pin.VersionText));
                    }
                }
            }
            else if (IsFileBasedApp(relativePath))
            {
                foreach (var directive in AppDirectiveEditor.ReadDirectives(path))
                {
                    if (directive.VersionText is { } versionText && BuildvanaFamily.Contains(directive.Id))
                    {
                        pins.Add(CreatePin(relativePath, directive.Id, versionText));
                    }
                }
            }
        }

        return pins;
    }

    /// <summary>
    /// Stamps the target version into the given pins and describes each one.
    /// </summary>
    /// <param name="pins">The pins to stamp, from <see cref="DiscoverPins"/>.</param>
    /// <param name="target">The version to stamp.</param>
    /// <returns>One display line per pin, in the order given: what changed, what was already at the target
    /// version, and what was left alone because its version is not a literal.</returns>
    /// <exception cref="BuildFailedException">A file could not be read or written.</exception>
    public IReadOnlyList<string> StampPins(IReadOnlyList<FamilyPin> pins, NuGetVersion target)
    {
        Guard.IsNotNull(pins);
        Guard.IsNotNull(target);
        foreach (var group in pins.GroupBy(static p => p.RelativePath))
        {
            var path = home.GetFullPath(group.Key);
            if (IsMsBuildFile(group.Key))
            {
                _ = MsBuildPinEditor.RewritePins(
                    path,
                    MsBuildItemTypes,
                    pin => BuildvanaFamily.Contains(pin.Id) ? NewVersionText(pin.VersionText, target) : null);
            }
            else
            {
                // The editor calls back only for directives that carry a version, so VersionText is never null here.
                _ = AppDirectiveEditor.RewriteVersions(
                    path,
                    directive => BuildvanaFamily.Contains(directive.Id) ? NewVersionText(directive.VersionText!, target) : null);
            }
        }

        return [.. pins.Select(pin => LineFor(pin, target))];
    }

    private static bool IsMsBuildFile(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        var isSharedFile = string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase);
        return isSharedFile || extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileBasedApp(string relativePath)
        => string.Equals(Path.GetExtension(relativePath), ".cs", StringComparison.OrdinalIgnoreCase);

    // The version parses only when its trimmed text is a literal version: surrounding whitespace, allowed in
    // a Version child element, is not part of the version.
    private static FamilyPin CreatePin(string relativePath, string id, string versionText)
    {
        var version = NuGetVersion.TryParse(versionText.Trim(), out var parsed) ? parsed : null;
        return new FamilyPin(relativePath, id, versionText, version);
    }

    // The new text for a pin's version value, or null to leave the pin alone: a non-literal version is not
    // this command's to move, and a version already at the target (by SemVer precedence, build metadata
    // ignored) is left byte-identical. Surrounding whitespace — part of a Version child element's raw
    // value — is preserved around the stamped version.
    private static string? NewVersionText(string versionText, NuGetVersion target)
    {
        var start = 0;
        while (start < versionText.Length && char.IsWhiteSpace(versionText[start]))
        {
            start++;
        }

        var end = versionText.Length;
        while (end > start && char.IsWhiteSpace(versionText[end - 1]))
        {
            end--;
        }

        var core = versionText[start..end];
        if (!NuGetVersion.TryParse(core, out var current) || VersionComparer.VersionRelease.Equals(current, target))
        {
            return null;
        }

        return versionText[..start] + target.ToNormalizedString() + versionText[end..];
    }

    private static string LineFor(FamilyPin pin, NuGetVersion target)
    {
        var targetText = target.ToNormalizedString();
        if (pin.Version is null)
        {
            return $"{pin.Id}: {pin.VersionText.Trim()} ({pin.RelativePath}, left alone)";
        }

        return VersionComparer.VersionRelease.Equals(pin.Version, target)
            ? $"{pin.Id}: {targetText} ({pin.RelativePath}, unchanged)"
            : $"{pin.Id}: {pin.Version.ToNormalizedString()} -> {targetText} ({pin.RelativePath})";
    }
}
