// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Computes a semantic version from version-file data, the git height, the current branch, and release policy.
/// </summary>
public static class VersionCalculator
{
    /// <summary>
    /// Computes the version to build.
    /// </summary>
    /// <param name="fileData">The data parsed from <c>current-version.json</c>.</param>
    /// <param name="height">The git height (number of commits since the version was last changed); must be non-negative.</param>
    /// <param name="branchName">The short name of the current branch, or the empty string when HEAD is detached.</param>
    /// <param name="publicReleaseBranches">The patterns identifying public-release branches, matched against
    /// <paramref name="branchName"/>. Callers should compile these with <see cref="RegexOptions.CultureInvariant"/>
    /// and a match timeout; the patterns are expected to carry their own anchors.</param>
    /// <param name="prereleaseTag">The prerelease tag to apply when <paramref name="fileData"/> denotes a prerelease;
    /// required (non-empty) in that case, ignored otherwise.</param>
    /// <returns>A <see cref="CalculatedVersion"/> describing the computed version.</returns>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="height"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="fileData"/> denotes a prerelease but
    /// <paramref name="prereleaseTag"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="RegexMatchTimeoutException">A pattern in <paramref name="publicReleaseBranches"/> exceeded its match timeout.</exception>
    public static CalculatedVersion Compute(
        VersionFileData fileData,
        int height,
        string branchName,
        IReadOnlyList<Regex> publicReleaseBranches,
        string? prereleaseTag)
    {
        Guard.IsNotNull(fileData);
        Guard.IsGreaterThanOrEqualTo(height, 0);
        Guard.IsNotNull(branchName);
        Guard.IsNotNull(publicReleaseBranches);

        SemanticVersion semanticVersion;
        if (fileData.Prerelease)
        {
            if (string.IsNullOrEmpty(prereleaseTag))
            {
                throw new ArgumentException(
                    "A prerelease tag is required to compute a prerelease version, but none was provided.",
                    nameof(prereleaseTag));
            }

            semanticVersion = new SemanticVersion(fileData.Major, fileData.Minor, height, prereleaseTag);
        }
        else
        {
            semanticVersion = new SemanticVersion(fileData.Major, fileData.Minor, height);
        }

        var currentStr = semanticVersion.ToNormalizedString();
        var isPublicRelease = publicReleaseBranches.Any(pattern => pattern.IsMatch(branchName));
        return new CalculatedVersion(semanticVersion, currentStr, isPublicRelease, semanticVersion.IsPrerelease);
    }
}
