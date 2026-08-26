// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Rewrites in-tree references to packages produced by the current build, so that a self-hosting (dogfooded)
/// project can bump its own SDK/tool/package references as part of the "Prepare release" commit.
/// </summary>
/// <remarks>
/// <para>The caller supplies the map of produced packages, typically obtained from
/// <see cref="Utilities.ArtifactsHelper.DiscoverProducedPackages"/>.</para>
/// <para>Updates are applied in-place to the following well-known files, when present:</para>
/// <list type="bullet">
///   <item><description><c>global.json</c> — entries under <c>msbuild-sdks</c>.</description></item>
///   <item><description><c>.config/dotnet-tools.json</c> — entries under <c>tools</c>.</description></item>
///   <item><description><c>Directory.Packages.props</c> — <c>&lt;PackageVersion&gt;</c> items.</description></item>
/// </list>
/// <para>Version values that look like MSBuild property references (e.g. <c>$(SomePackageVersion)</c>) are
/// left untouched, since rewriting them would break the indirection.</para>
/// </remarks>
internal sealed class SelfReferenceUpdater
{
    private readonly IReporter _reporter;
    private readonly IHomeDirectoryProvider _home;
    private readonly IJsonHelper _jsonHelper;
    private readonly (string RelativePath, Func<string, IReadOnlyDictionary<string, string>, bool> Update)[] _targets;

    public SelfReferenceUpdater(
        IReporter reporter,
        IHomeDirectoryProvider home,
        IJsonHelper jsonHelper)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(home);
        Guard.IsNotNull(jsonHelper);
        _reporter = reporter;
        _home = home;
        _jsonHelper = jsonHelper;
        _targets =
        [
            ("global.json", (p, produced) => UpdateJsonContainer(p, produced, container: "msbuild-sdks", versionPropertyName: null)),
            (".config/dotnet-tools.json", (p, produced) => UpdateJsonContainer(
                p,
                produced,
                container: "tools",
                versionPropertyName: "version")),
            ("Directory.Packages.props", (p, produced) => UpdateMsBuildXml(p, produced, itemTypes: ["PackageVersion"])),
        ];
    }

    /// <summary>
    /// Rewrites in-tree references to packages produced by the current build.
    /// </summary>
    /// <param name="producedPackages">The packages produced by the current build, as a map from package ID to version.</param>
    /// <returns>The list of files that were actually modified. Pass this to
    /// <see cref="ServerAdapters.ServerRelease.AddPostReleaseCommit(string, string[])"/> to commit them
    /// into a separate post-release commit on top of the "Prepare release" commit.</returns>
    public IReadOnlyList<string> UpdateReferences(IReadOnlyDictionary<string, string> producedPackages)
    {
        Guard.IsNotNull(producedPackages);
        if (producedPackages.Count == 0)
        {
            _reporter.Info("Self-reference update: no produced packages.");
            return [];
        }

        _reporter.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Self-reference update: {producedPackages.Count} produced package(s): {string.Join(", ", producedPackages.Keys)}."));

        var modified = new List<string>();
        foreach (var (relativePath, update) in _targets)
        {
            // Resolve up-front so the path returned to the caller (and shown in logs) is unambiguous.
            var path = _home.GetFullPath(relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            if (update(path, producedPackages))
            {
                _reporter.Info($"Self-reference update: rewrote {relativePath}.");
                modified.Add(path);
            }
        }

        return modified;
    }

    // Splice the new version directly over the existing one in the source bytes, so unrelated
    // bytes — line endings, indentation, the trailing newline (if any), comments, BOM — survive untouched.
    // The expected location of each version string differs by container shape:
    //   - versionPropertyName == null → at depth 2 with path [container, packageId];
    //   - versionPropertyName != null → at depth 3 with path [container, packageId, versionPropertyName].
    private bool UpdateJsonContainer(
        string path,
        IReadOnlyDictionary<string, string> produced,
        string container,
        string? versionPropertyName)
        => _jsonHelper.RewriteStringValues(path, (propertyPath, currentValue) =>
        {
            if (versionPropertyName is null)
            {
                if (propertyPath.Count != 2 || propertyPath[0] != container)
                {
                    return null;
                }
            }
            else
            {
                if (propertyPath.Count != 3 || propertyPath[0] != container || propertyPath[2] != versionPropertyName)
                {
                    return null;
                }
            }

            var packageId = propertyPath[1];
            return produced.TryGetValue(packageId, out var newVersion) && !string.Equals(currentValue, newVersion, StringComparison.Ordinal)
                ? newVersion
                : null;
        });

    private bool UpdateMsBuildXml(string path, IReadOnlyDictionary<string, string> produced, string[] itemTypes)
        => MsBuildPinEditor.RewritePins(path, itemTypes, pin =>
        {
            if (!produced.TryGetValue(pin.Id, out var newVersion))
            {
                return null;
            }

            // Don't rewrite property references like $(SomeVersion) — they'd silently lose their indirection.
            if (pin.VersionText.Contains("$(", StringComparison.Ordinal))
            {
                _reporter.Detail(
                    $"Self-reference update: leaving property-reference version '{pin.VersionText}' on package '{pin.Id}' unchanged.");
                return null;
            }

            return newVersion;
        });
}
