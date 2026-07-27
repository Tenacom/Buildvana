// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CommunityToolkit.Diagnostics;
using LibGit2Sharp;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Computes the Git height of a version line: the number of commits in the longest parent-path from
/// <c>HEAD</c> (inclusive) across consecutive commits whose committed version file parses to the same
/// <c>MAJOR.MINOR</c> as the version being built. The walk stops where the version file is absent,
/// malformed, or differs in <c>MAJOR.MINOR</c>; prerelease-only differences do not stop it.
/// </summary>
/// <remarks>
/// <para>The height rules mirror Nerdbank.GitVersioning (© .NET Foundation and Contributors, MIT license;
/// see <c>LibGit2GitExtensions.GetHeight</c> and <c>SemanticVersion.WillVersionChangeResetVersionHeight</c>
/// at https://github.com/dotnet/Nerdbank.GitVersioning): each qualifying commit contributes 1 plus the
/// maximum height among its parents, so the commit that bumps <c>MAJOR.MINOR</c> has height 1, and 0 is
/// reserved for version lines with no committed history. Unlike NBGV, a commit qualifies based on the
/// version file's <c>MAJOR.MINOR</c> alone: there are no path filters, and the version file is plain text
/// rather than JSON.</para>
/// <para>Heights are computed from the available commit graph; shallow clones therefore yield short heights.</para>
/// <para>This class is the library's only Git touchpoint; its public surface deliberately exposes no
/// LibGit2Sharp types.</para>
/// </remarks>
public sealed class GitHeightCalculator
{
    private readonly string _versionFileName;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHeightCalculator"/> class.
    /// </summary>
    /// <param name="versionFileName">The name of the version file, relative to the repository root.</param>
    public GitHeightCalculator(string versionFileName)
    {
        Guard.IsNotNullOrEmpty(versionFileName);
        _versionFileName = versionFileName;
    }

    /// <summary>
    /// Calculates the Git height of the version line identified by <paramref name="expected"/> in the
    /// repository at <paramref name="repositoryDirectory"/>, along with the repository facts needed to
    /// compute version strings.
    /// </summary>
    /// <param name="repositoryDirectory">The directory of the Git repository.</param>
    /// <param name="expected">The version specification being built; only <see cref="VersionSpec.Major"/>
    /// and <see cref="VersionSpec.Minor"/> take part in the computation.</param>
    /// <returns>A newly-created <see cref="GitHeightResult"/>.</returns>
    /// <exception cref="BuildFailedException">There is no Git repository at <paramref name="repositoryDirectory"/>.</exception>
    public GitHeightResult Calculate(string repositoryDirectory, VersionSpec expected)
    {
        Guard.IsNotNullOrEmpty(repositoryDirectory);
        Guard.IsNotNull(expected);
        BuildFailedException.ThrowIfNot(Repository.IsValid(repositoryDirectory), $"There is no Git repository at {repositoryDirectory}");
        using var repository = new Repository(repositoryDirectory);
        var head = repository.Head;
        var tip = head.Tip;
        if (tip is null)
        {
            return new GitHeightResult(0, string.Empty, null);
        }

        var isOnBranch = head.CanonicalName.StartsWith("refs/heads/", StringComparison.Ordinal);
        var branchName = isOnBranch ? head.FriendlyName : string.Empty;
        return new GitHeightResult(ComputeHeight(tip, expected), branchName, tip.Sha);
    }

    private int ComputeHeight(Commit tip, VersionSpec expected)
    {
        // Iterative post-order walk with memoization: a commit's height is computed only after all its
        // parents'. Commits outside the version line get height 0 without visiting their parents, so the
        // walk is bounded by the size of the line plus its immediate frontier.
        var heights = new Dictionary<ObjectId, int>();
        var lineBlobs = new Dictionary<ObjectId, bool>();
        var stack = new Stack<Commit>();
        stack.Push(tip);
        while (stack.Count > 0)
        {
            var commit = stack.Peek();
            if (heights.ContainsKey(commit.Id))
            {
                _ = stack.Pop();
                continue;
            }

            if (!IsInLine(commit, expected, lineBlobs))
            {
                heights[commit.Id] = 0;
                _ = stack.Pop();
                continue;
            }

            var maxParentHeight = 0;
            var hasPendingParents = false;
            foreach (var parent in commit.Parents)
            {
                if (heights.TryGetValue(parent.Id, out var parentHeight))
                {
                    maxParentHeight = Math.Max(maxParentHeight, parentHeight);
                }
                else
                {
                    stack.Push(parent);
                    hasPendingParents = true;
                }
            }

            if (!hasPendingParents)
            {
                heights[commit.Id] = 1 + maxParentHeight;
                _ = stack.Pop();
            }
        }

        return heights[tip.Id];
    }

    private bool IsInLine(Commit commit, VersionSpec expected, Dictionary<ObjectId, bool> lineBlobs)
    {
        var entry = commit[_versionFileName];
        if (entry is not { TargetType: TreeEntryTargetType.Blob })
        {
            return false;
        }

        var blobId = entry.Target.Id;
        if (lineBlobs.TryGetValue(blobId, out var isInLine))
        {
            return isInLine;
        }

        var content = ((Blob)entry.Target).GetContentText();
        var hasBom = content.Length > 0 && content[0] == 0xFEFF;
        if (hasBom)
        {
            // Tolerate a UTF-8 byte order mark in a committed version file: the parser's regex would reject it.
            content = content[1..];
        }

        isInLine = VersionSpec.TryParse(content, out var spec)
            && spec.Major == expected.Major
            && spec.Minor == expected.Minor;
        lineBlobs[blobId] = isInLine;
        return isInLine;
    }
}
