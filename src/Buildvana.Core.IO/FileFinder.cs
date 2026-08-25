// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Buildvana.Core.Diagnostics;
using Buildvana.Core.IO.Gitignore;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.IO;

/// <summary>
/// Enumerates the files under a base directory that gitignore rules leave visible, for machinery that
/// must see a repository's own files and never build debris.
/// </summary>
/// <remarks>
/// <para>The finder walks the directory tree, reading each directory's <c>.gitignore</c> on the way down
/// and applying the <see href="https://git-scm.com/docs/gitignore">gitignore(5)</see> rules: within one
/// file the last matching pattern decides, a file in a
/// deeper directory overrides its ancestors, and an ignored directory is pruned outright — which is also
/// why a negation cannot re-include anything beneath one. No repository detection is involved:
/// <c>.gitignore</c> files take effect wherever they are found, and the walk is the same inside and
/// outside a Git repository. The one name treated specially is <c>.git</c>: like Git itself
/// (<c>dir.c</c>'s <c>treat_path</c>), the finder skips any entry so named — file or directory, at every
/// level — before consulting any pattern, which is why no <c>.gitignore</c> ever needs to list it.
/// Beyond per-directory files, Git also reads patterns from
/// <c>$GIT_DIR/info/exclude</c> and <c>core.excludesFile</c>; both belong to one user's setup rather than
/// to the repository's content, and the finder deliberately ignores them. Likewise, when the base
/// directory sits inside a repository, <c>.gitignore</c> files in directories above it are not read:
/// the walk — and the patterns it honors — starts at the base directory.</para>
/// <para>Exclusion patterns passed to the constructor are interpreted as if written in a
/// <c>.gitignore</c> at the base directory, and take unconditional precedence: a <c>.gitignore</c>
/// negation cannot re-include a path they exclude. Negations within the exclusion list itself work
/// normally against earlier exclusion patterns.</para>
/// <para>Directories that are reparse points (symbolic links, junctions) are neither descended nor
/// listed, so the walk cannot cycle. Files that are symbolic links are listed like any other file. Git
/// instead records a directory symbolic link as an entry; a consumer of this finder wants readable files,
/// not link metadata, so the deviation is deliberate.</para>
/// </remarks>
public sealed class FileFinder
{
    private readonly string _basePath;
    private readonly GitignoreFile _exclusions;
    private readonly bool _ignoresCase;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileFinder"/> class.
    /// </summary>
    /// <param name="basePath">The directory to enumerate. May be relative; resolved against the process
    /// working directory.</param>
    /// <param name="exclusionPatterns">Gitignore-syntax patterns excluded on top of what
    /// <c>.gitignore</c> files dictate, interpreted as if written in a <c>.gitignore</c> at
    /// <paramref name="basePath"/>; see the class remarks for their precedence.</param>
    /// <param name="caseSensitivity">How pattern matching treats letter case.</param>
    public FileFinder(
        string basePath,
        IReadOnlyList<string>? exclusionPatterns = null,
        CaseSensitivityMode caseSensitivity = CaseSensitivityMode.SystemDefault)
    {
        Guard.IsNotNullOrEmpty(basePath);
        _basePath = Path.GetFullPath(basePath);
        _exclusions = GitignoreFile.Parse(exclusionPatterns ?? []);
        _ignoresCase = caseSensitivity.IgnoresCase();
    }

    /// <summary>
    /// Walks the base directory and returns the files that survive the gitignore rules and the
    /// exclusion patterns.
    /// </summary>
    /// <returns>The paths of the surviving files, relative to the base directory, with <c>/</c> as the
    /// separator, in depth-first order with each directory's entries sorted ordinally.</returns>
    /// <exception cref="BuildFailedException">A directory could not be enumerated, or a
    /// <c>.gitignore</c> file could not be read.</exception>
    /// <remarks>
    /// <para>The result is fully materialized, so failures are raised at call time. A non-existent base
    /// directory yields an empty list rather than a failure.</para>
    /// </remarks>
    public IReadOnlyList<string> GetFiles()
    {
        var result = new List<string>();
        if (!UserDirectory.Exists(_basePath))
        {
            return result;
        }

        Walk(new DirectoryInfo(_basePath), [], [], result);
        return result;
    }

    private static GitignoreFile? ReadGitignoreFile(DirectoryInfo directory)
    {
        var path = Path.Combine(directory.FullName, ".gitignore");
        return UserFile.Exists(path) ? GitignoreFile.Parse(UserFile.ReadAllLines(path)) : null;
    }

    private static FileSystemInfo[] GetEntries(DirectoryInfo directory)
    {
        try
        {
            var entries = directory.GetFileSystemInfos();
            Array.Sort(entries, static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return entries;
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not enumerate directory {directory.FullName}: {e.Message}", e);
        }
    }

    private void Walk(
        DirectoryInfo directory,
        List<string> components,
        List<(GitignoreFile File, int Depth)> gitignoreStack,
        List<string> result)
    {
        var gitignoreFile = ReadGitignoreFile(directory);
        if (gitignoreFile is not null)
        {
            gitignoreStack.Add((gitignoreFile, components.Count));
        }

        foreach (var entry in GetEntries(directory))
        {
            if (IsGitEntry(entry.Name))
            {
                continue;
            }

            var isDirectory = entry is DirectoryInfo;
            components.Add(entry.Name);
            if (IsIncluded(components, gitignoreStack, isDirectory))
            {
                if (!isDirectory)
                {
                    result.Add(string.Join('/', components));
                }
                else if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    Walk((DirectoryInfo)entry, components, gitignoreStack, result);
                }
            }

            components.RemoveAt(components.Count - 1);
        }

        if (gitignoreFile is not null)
        {
            gitignoreStack.RemoveAt(gitignoreStack.Count - 1);
        }
    }

    // Git never descends into ".git": dir.c's treat_path drops any entry so named, at every level, before
    // consulting any pattern. The comparison honors the finder's case mode, as Git's fspathcmp honors
    // core.ignoreCase. Matching by name rather than by kind also covers submodule and worktree gitlinks,
    // where ".git" is a file.
    private bool IsGitEntry(string name)
    {
        var comparison = _ignoresCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(name, ".git", comparison);
    }

    private bool IsIncluded(
        List<string> components,
        List<(GitignoreFile File, int Depth)> gitignoreStack,
        bool isDirectory)
    {
        var path = CollectionsMarshal.AsSpan(components);
        if (_exclusions.Evaluate(path, isDirectory, _ignoresCase) == GitignoreDecision.Ignore)
        {
            return false;
        }

        // Deeper .gitignore files override shallower ones, so the innermost file with an opinion decides.
        // Each file sees the path relative to its own directory.
        for (var i = gitignoreStack.Count - 1; i >= 0; i--)
        {
            var (file, depth) = gitignoreStack[i];
            var decision = file.Evaluate(path[depth..], isDirectory, _ignoresCase);
            if (decision != GitignoreDecision.None)
            {
                return decision == GitignoreDecision.Include;
            }
        }

        return true;
    }
}
