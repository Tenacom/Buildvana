// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Json;
using Buildvana.Tool.Services.Versioning;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

using SysDirectory = System.IO.Directory;
using SysFile = System.IO.File;
using SysPath = System.IO.Path;

namespace Buildvana.Tool.Services;

/// <summary>
/// Rewrites in-tree references to packages produced by the current build, so that a self-hosting (dogfooded)
/// project can bump its own SDK/tool/package references as part of the "Prepare release" commit.
/// </summary>
/// <remarks>
/// <para>The updater discovers produced packages by inspecting the <c>*.nupkg</c> files in the artifacts directory:
/// each filename is expected to match the form <c>{Id}.{CurrentVersion}.nupkg</c>; files whose version suffix
/// does not match the version being released are ignored.</para>
/// <para>Updates are applied in-place to the following well-known files, when present:</para>
/// <list type="bullet">
///   <item><description><c>global.json</c> — entries under <c>msbuild-sdks</c>.</description></item>
///   <item><description><c>.config/dotnet-tools.json</c> — entries under <c>tools</c>.</description></item>
///   <item><description><c>Directory.Packages.props</c> — <c>&lt;PackageVersion&gt;</c> items.</description></item>
/// </list>
/// <para>Version values that look like MSBuild property references (e.g. <c>$(SomePackageVersion)</c>) are
/// left untouched, since rewriting them would break the indirection.</para>
/// </remarks>
public sealed class SelfReferenceUpdater
{
    private readonly ILogger<SelfReferenceUpdater> _logger;
    private readonly IHomeDirectoryProvider _home;
    private readonly IJsonHelper _jsonHelper;
    private readonly DotNetService _dotnet;
    private readonly VersionService _version;
    private readonly (string RelativePath, Func<FilePath, Dictionary<string, string>, bool> Update)[] _targets;

    public SelfReferenceUpdater(
        ILogger<SelfReferenceUpdater> logger,
        IHomeDirectoryProvider home,
        IJsonHelper jsonHelper,
        DotNetService dotnet,
        VersionService version)
    {
        Guard.IsNotNull(logger);
        Guard.IsNotNull(home);
        Guard.IsNotNull(jsonHelper);
        Guard.IsNotNull(dotnet);
        Guard.IsNotNull(version);
        _logger = logger;
        _home = home;
        _jsonHelper = jsonHelper;
        _dotnet = dotnet;
        _version = version;
        _targets =
        [
            ("global.json", (p, produced) => UpdateJsonContainer(p, produced, container: "msbuild-sdks", versionPropertyName: null)),
            (".config/dotnet-tools.json", (p, produced) => UpdateJsonContainer(p, produced, container: "tools", versionPropertyName: "version")),
            ("Directory.Packages.props", (p, produced) => UpdateMsBuildXml(p, produced, tagNames: ["PackageVersion"])),
        ];
    }

    /// <summary>
    /// Rewrites in-tree references to packages produced by the current build.
    /// </summary>
    /// <returns>The list of files that were actually modified. Pass this to
    /// <see cref="ServerAdapters.ServerRelease.AddPostReleaseCommit(string, FilePath[])"/> to commit them
    /// into a separate post-release commit on top of the "Prepare release" commit.</returns>
    public IReadOnlyList<FilePath> UpdateReferences()
    {
        var produced = DiscoverProducedPackages();
        if (produced.Count == 0)
        {
            _logger.LogInformation("Self-reference update: no produced packages were found in the artifacts directory.");
            return [];
        }

        _logger.LogInformation(
            "Self-reference update: {Count} produced package(s) detected: {Packages}.",
            produced.Count,
            string.Join(", ", produced.Keys));

        var modified = new List<FilePath>();
        var homeDirectory = new DirectoryPath(_home.HomeDirectory);
        foreach (var (relativePath, update) in _targets)
        {
            // FilePath.FullPath of a relative path is still relative; resolve up-front so the path
            // returned to the caller (and shown in logs) is unambiguous.
            var path = new FilePath(relativePath).MakeAbsolute(homeDirectory);
            if (!SysFile.Exists(path.FullPath))
            {
                continue;
            }

            if (update(path, produced))
            {
                _logger.LogInformation("Self-reference update: rewrote {RelativePath}.", relativePath);
                modified.Add(path);
            }
        }

        return modified;
    }

    private Dictionary<string, string> DiscoverProducedPackages()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var artifacts = _dotnet.ArtifactsPath.FullPath;
        if (!SysDirectory.Exists(artifacts))
        {
            return result;
        }

        var version = _version.CurrentStr;
        var suffix = $".{version}.nupkg";
        foreach (var path in SysDirectory.EnumerateFiles(artifacts, "*.nupkg"))
        {
            var fileName = SysPath.GetFileName(path);
            if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Self-reference update: skipping '{FileName}' (version does not match '{Version}').", fileName, version);
                continue;
            }

            var id = fileName[..^suffix.Length];
            result[id] = version;
        }

        return result;
    }

    // Splice the new version directly over the existing one in the source bytes, so unrelated
    // bytes — line endings, indentation, the trailing newline (if any), comments, BOM — survive untouched.
    // The expected location of each version string differs by container shape:
    //   - versionPropertyName == null → at depth 2 with path [container, packageId];
    //   - versionPropertyName != null → at depth 3 with path [container, packageId, versionPropertyName].
    private bool UpdateJsonContainer(FilePath path, Dictionary<string, string> produced, string container, string? versionPropertyName)
        => _jsonHelper.RewriteStringValues(path.FullPath, (propertyPath, currentValue) =>
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

    private bool UpdateMsBuildXml(FilePath path, Dictionary<string, string> produced, string[] tagNames)
    {
        var fullPath = path.FullPath;

        // Read while detecting the file's encoding from any BOM, and remember it so the rewrite
        // preserves the original encoding exactly. The fallback when no BOM is present is UTF-8
        // without BOM; using the static Encoding.UTF8 as fallback (which has emitBOM=true) would
        // silently add a BOM on rewrite to files that did not have one.
        string original;
        Encoding encoding;
        using (var reader = new System.IO.StreamReader(fullPath, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true))
        {
            original = reader.ReadToEnd();
            encoding = reader.CurrentEncoding;
        }

        // Build a regex alternation from the supplied tag names.
        // The two patterns are mutually exclusive: each matching start tag has Include and Version
        // attributes in exactly one order, so a given match site is matched by at most one of them.
        var tagAlternation = string.Join('|', Array.ConvertAll(tagNames, Regex.Escape));
        var includeFirst = new Regex(
            $@"(<(?:{tagAlternation})\b[^>]*?\bInclude\s*=\s*"")([^""]+)(""[^>]*?\bVersion\s*=\s*"")([^""]*)("")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var versionFirst = new Regex(
            $@"(<(?:{tagAlternation})\b[^>]*?\bVersion\s*=\s*"")([^""]*)(""[^>]*?\bInclude\s*=\s*"")([^""]+)("")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var current = original;
        current = includeFirst.Replace(current, m => RewriteIncludeFirstMatch(m, produced));
        current = versionFirst.Replace(current, m => RewriteVersionFirstMatch(m, produced));

        if (string.Equals(current, original, StringComparison.Ordinal))
        {
            return false;
        }

        SysFile.WriteAllText(fullPath, current, encoding);
        return true;
    }

    private string RewriteIncludeFirstMatch(Match match, Dictionary<string, string> produced)
    {
        // Groups: 1 = head up to opening quote of Include value
        //         2 = Include value (package id)
        //         3 = middle up to opening quote of Version value
        //         4 = Version value (current)
        //         5 = closing quote of Version value
        var id = match.Groups[2].Value;
        if (!produced.TryGetValue(id, out var newVersion))
        {
            return match.Value;
        }

        var existing = match.Groups[4].Value;
        if (!IsRewritable(existing, id, newVersion))
        {
            return match.Value;
        }

        return match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value + newVersion + match.Groups[5].Value;
    }

    private string RewriteVersionFirstMatch(Match match, Dictionary<string, string> produced)
    {
        // Groups: 1 = head up to opening quote of Version value
        //         2 = Version value (current)
        //         3 = middle up to opening quote of Include value
        //         4 = Include value (package id)
        //         5 = closing quote of Include value
        var id = match.Groups[4].Value;
        if (!produced.TryGetValue(id, out var newVersion))
        {
            return match.Value;
        }

        var existing = match.Groups[2].Value;
        if (!IsRewritable(existing, id, newVersion))
        {
            return match.Value;
        }

        return match.Groups[1].Value + newVersion + match.Groups[3].Value + match.Groups[4].Value + match.Groups[5].Value;
    }

    private bool IsRewritable(string existing, string id, string newVersion)
    {
        // Don't rewrite property references like $(SomeVersion) — they'd silently lose their indirection.
        if (existing.Contains("$(", StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "Self-reference update: leaving property-reference version '{Existing}' on package '{Id}' unchanged.",
                existing,
                id);
            return false;
        }

        return !string.Equals(existing, newVersion, StringComparison.Ordinal);
    }
}
