// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buildvana.Core.Diagnostics;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Buildvana.Core.IO;

/// <summary>
/// <para>Enumerates the files of a directory tree the way Git sees it: nested gitignore files are honored,
/// whether or not the tree is an actual Git repository; an ignored directory is never entered; and a
/// caller-supplied set of well-known exclusions is skipped unconditionally, files and directories alike.</para>
/// <para>The deliberate divergences from Git itself: a path matching an ignore pattern is skipped even when
/// a Git index tracks it; and per-machine exclude sources (<c>$GIT_DIR/info/exclude</c>,
/// <c>core.excludesFile</c>) are not consulted, so the result depends on nothing but the tree's own
/// content.</para>
/// </summary>
public sealed class FileFinder
{
    private readonly string _rootPath;
    private readonly HashSet<string> _excludedRootPaths;
    private readonly HashSet<string> _excludedNames;
    private readonly MatchCasing _matchCasing;
    private readonly StringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileFinder"/> class.
    /// </summary>
    /// <param name="rootPath">The root of the tree to enumerate.</param>
    /// <param name="excludedRootPaths">Root-relative, slash-separated paths to skip unconditionally,
    /// e.g. an artifacts directory.</param>
    /// <param name="excludedNames">Names to skip unconditionally at every depth,
    /// e.g. <c>.git</c> or <c>bin</c>.</param>
    /// <param name="matchCasing">How glob matching, gitignore patterns, and the exclusions treat character
    /// casing; see <see cref="GitIgnorePattern.TryParse(string, MatchCasing, out GitIgnorePattern)"/>.</param>
    public FileFinder(
        string rootPath,
        IEnumerable<string> excludedRootPaths,
        IEnumerable<string> excludedNames,
        MatchCasing matchCasing = MatchCasing.CaseSensitive)
    {
        Guard.IsNotNullOrEmpty(rootPath);
        Guard.IsNotNull(excludedRootPaths);
        Guard.IsNotNull(excludedNames);
        _rootPath = Path.GetFullPath(rootPath);
        _matchCasing = matchCasing;
        _comparison = matchCasing.IsCaseInsensitive() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var comparer = StringComparer.FromComparison(_comparison);
        _excludedRootPaths = new(excludedRootPaths, comparer);
        _excludedNames = new(excludedNames, comparer);
    }

    /// <summary>
    /// Lazily enumerates all files under the root.
    /// </summary>
    /// <returns>Root-relative, slash-separated file paths: the files of each directory in ordinal name
    /// order, followed by the contents of its subdirectories, themselves in ordinal name order.</returns>
    /// <remarks>
    /// <para>The walk advances with the enumeration, so the tree is read as results are consumed, and a
    /// failure to read it - unlike one in <see cref="UserDirectory.EnumerateFiles"/>, which materializes
    /// its result for this very reason - surfaces as a <see cref="BuildFailedException"/> during enumeration
    /// rather than at call time.</para>
    /// <para>Directory symbolic links and junctions are not followed.</para>
    /// </remarks>
    public IEnumerable<string> FindFiles()
        => EnumerateDirectory(_rootPath, string.Empty, []);

    /// <summary>
    /// Lazily enumerates the files under the root that match a glob.
    /// </summary>
    /// <param name="glob">A glob pattern, e.g. <c>**/*.cs</c>, applied to root-relative paths.</param>
    /// <returns>The matching subsequence of what <see cref="FindFiles()"/> returns.</returns>
    public IEnumerable<string> FindFiles(string glob)
    {
        Guard.IsNotNullOrEmpty(glob);
        var matcher = new Matcher(_comparison);
        _ = matcher.AddInclude(glob);
        return FindFiles().Where(path => matcher.Match(path).HasMatches);
    }

    private static bool IsIgnored(
        (GitIgnorePatternList Patterns, int BaseLength)[] scopes,
        string relativePath,
        bool isDirectory)
    {
        // Deeper gitignore files take precedence, and the first list with an opinion decides; there is no
        // ancestor check here because the walk never descends into an ignored directory in the first place.
        for (var i = scopes.Length - 1; i >= 0; i--)
        {
            var (patterns, baseLength) = scopes[i];
            var decision = patterns.GetDecision(relativePath[baseLength..], isDirectory);
            if (decision != GitIgnoreDecision.None)
            {
                return decision == GitIgnoreDecision.Ignore;
            }
        }

        return false;
    }

    private static string[]? TryReadGitIgnore(string directoryPath)
    {
        var path = Path.Combine(directoryPath, ".gitignore");
        try
        {
            return File.Exists(path) ? File.ReadAllLines(path) : null;
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not read {path}: {e.Message}", e);
        }
    }

    private static (string[] Files, string[] Directories) GetEntries(string directoryPath)
    {
        try
        {
            var info = new DirectoryInfo(directoryPath);
            var files = info.EnumerateFiles()
                .Select(static x => x.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var directories = info.EnumerateDirectories()
                .Where(static x => (x.Attributes & FileAttributes.ReparsePoint) == 0)
                .Select(static x => x.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return (files, directories);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not enumerate files in {directoryPath}: {e.Message}", e);
        }
    }

    private static string Combine(string relativePath, string name)
        => relativePath.Length == 0 ? name : relativePath + "/" + name;

    private IEnumerable<string> EnumerateDirectory(
        string directoryPath,
        string relativePath,
        (GitIgnorePatternList Patterns, int BaseLength)[] parentScopes)
    {
        var scopes = parentScopes;
        var gitIgnoreLines = TryReadGitIgnore(directoryPath);
        if (gitIgnoreLines is not null)
        {
            // Patterns are relative to the directory of their gitignore file: the scope records how much
            // of a root-relative path to strip before matching against this list.
            var baseLength = relativePath.Length == 0 ? 0 : relativePath.Length + 1;
            scopes = [.. parentScopes, (GitIgnorePatternList.Parse(gitIgnoreLines, _matchCasing), baseLength)];
        }

        var (files, directories) = GetEntries(directoryPath);
        foreach (var name in files)
        {
            var filePath = Combine(relativePath, name);
            var isSkipped = _excludedNames.Contains(name)
                || _excludedRootPaths.Contains(filePath)
                || IsIgnored(scopes, filePath, isDirectory: false);
            if (!isSkipped)
            {
                yield return filePath;
            }
        }

        foreach (var name in directories)
        {
            var directoryRelativePath = Combine(relativePath, name);
            var isSkipped = _excludedNames.Contains(name)
                || _excludedRootPaths.Contains(directoryRelativePath)
                || IsIgnored(scopes, directoryRelativePath, isDirectory: true);
            if (isSkipped)
            {
                continue;
            }

            var subResults = EnumerateDirectory(Path.Combine(directoryPath, name), directoryRelativePath, scopes);
            foreach (var result in subResults)
            {
                yield return result;
            }
        }
    }
}
