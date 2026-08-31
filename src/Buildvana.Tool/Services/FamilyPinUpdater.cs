// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;
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
/// <c>PackageVersion</c>, <c>GlobalPackageReference</c>, and <c>PackageReference</c>, plus the item name of
/// every additional pin group the configuration declares — a family pin declared under a repository's own
/// item name is a family pin like any other, and lockstep does not admit exceptions; a
/// <c>VersionOverride</c> is never read or stamped, family ids included — an override overrules a
/// dependency update, and self-update is one, so whoever writes an override owns the version and its
/// consequences, drift out of lockstep included. The summary does not mention what is not ours to move.
/// Directives are read from <c>.cs</c> files within the
/// file-based-app scope (<see cref="BuildvanaConfig.FileBasedApps"/>): reading every <c>.cs</c> file would
/// make discovery cost scale with the whole source tree, and the declared scope keeps the summary an honest
/// coverage check — a directive outside it is out of scope by the user's own statement. A versionless
/// directive is a reference to a pin declared elsewhere, not a pin, and is not seen.</para>
/// <para>The scope and the item names are read from the resolved configuration leniently: when the
/// configuration cannot be read, discovery falls back to the built-in hooks scope and the built-in item
/// types with a warning instead of failing — self-update is the tool that repairs a half-updated
/// repository, so it must run on one whose configuration file predates it.</para>
/// </remarks>
internal sealed class FamilyPinUpdater(IHomeDirectoryProvider home, Lazy<BuildvanaConfig> config, IReporter reporter)
{
    // Both the file-based-app scope and the item names come from the configuration, and reading it can warn.
    // Reading it once is what keeps a repository whose configuration file cannot be read from being warned
    // about twice in the same run.
    private readonly Lazy<BuildvanaConfig> _resolvedConfig = new(() => ResolveConfig(config, reporter));

    /// <summary>
    /// Walks the home directory and returns every family pin found, in walk order.
    /// </summary>
    /// <returns>The pins found.</returns>
    /// <exception cref="BuildFailedException">A directory or file could not be read.</exception>
    public IReadOnlyList<FamilyPin> DiscoverPins()
    {
        var scope = ResolveScope();
        var itemTypes = ResolveItemTypes();
        var pins = new List<FamilyPin>();
        foreach (var relativePath in RepositoryFiles.CreateFinder(home).GetFiles())
        {
            var path = home.GetFullPath(relativePath);
            if (IsMsBuildFile(relativePath))
            {
                foreach (var pin in MsBuildPinEditor.ReadPins(path, itemTypes))
                {
                    if (BuildvanaFamily.Contains(pin.Id))
                    {
                        pins.Add(CreatePin(relativePath, pin.Id, pin.VersionText));
                    }
                }
            }
            else if (scope.Contains(relativePath))
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
    /// Stamps the target version into the repository's family pins and describes each given pin.
    /// </summary>
    /// <param name="pins">The pins discovered by <see cref="DiscoverPins"/>. They select the files to edit
    /// and shape the summary; the rewrite itself re-reads each file and stamps every family pin found in it,
    /// so a subset of a file's pins cannot be stamped alone.</param>
    /// <param name="target">The version to stamp.</param>
    /// <returns>One display line per given pin, in the order given: what changed, what was already at the
    /// target version, and what was left alone because its version is not a literal.</returns>
    /// <exception cref="BuildFailedException">A file could not be read or written.</exception>
    public IReadOnlyList<string> StampPins(IReadOnlyList<FamilyPin> pins, NuGetVersion target)
    {
        Guard.IsNotNull(pins);
        Guard.IsNotNull(target);
        var itemTypes = ResolveItemTypes();
        foreach (var group in pins.GroupBy(static p => p.RelativePath))
        {
            var path = home.GetFullPath(group.Key);
            if (IsMsBuildFile(group.Key))
            {
                _ = MsBuildPinEditor.RewritePins(
                    path,
                    itemTypes,
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

    // A configuration file this bv cannot read degrades discovery to the built-in scope and item types
    // instead of killing the update, since self-update is the tool that repairs the repository. The
    // configuration problems themselves are reported by the post-update validation, so the warning here only
    // names the degradation.
    private static BuildvanaConfig ResolveConfig(Lazy<BuildvanaConfig> config, IReporter reporter)
    {
        try
        {
            return config.Value;
        }
        catch (BuildFailedException)
        {
            reporter.Warning(
                "The configuration file could not be read; file-based apps are searched only under "
                + $"{WellKnownPaths.HooksDirectory}, and package pins only under the built-in item types.");
            return new BuildvanaConfig();
        }
    }

    private FileBasedAppScope ResolveScope() => FileBasedAppScope.Parse(_resolvedConfig.Value.FileBasedApps);

    // An additional pin group declares the item name its pins are written as, and a family pin written that
    // way is a family pin like any other. Names are compared case-insensitively, as MSBuild compares item
    // names, so a group that names a built-in type adds nothing.
    private IReadOnlyList<string> ResolveItemTypes()
    {
        var groups = _resolvedConfig.Value.Dependencies.AdditionalPackages;
        if (groups.Count == 0)
        {
            return PackageItemTypes.BuiltIn;
        }

        var itemTypes = new List<string>(PackageItemTypes.BuiltIn);
        foreach (var group in groups)
        {
            if (!itemTypes.Contains(group.Items, StringComparer.OrdinalIgnoreCase))
            {
                itemTypes.Add(group.Items);
            }
        }

        return [.. itemTypes];
    }
}
