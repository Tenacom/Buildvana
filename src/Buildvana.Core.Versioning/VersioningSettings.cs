// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Buildvana.Runtime;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Resolves versioning settings from a <see cref="BuildvanaConfig"/>: the public-release branch patterns
/// (from the <c>release</c> section), plus the prerelease tag and the assembly-version precision (from the
/// <c>versioning</c> section).
/// </summary>
public sealed class VersioningSettings
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="VersioningSettings"/> class.
    /// </summary>
    /// <param name="config">The Buildvana configuration to read the <c>release</c> and <c>versioning</c> sections from.</param>
    public VersioningSettings(BuildvanaConfig config)
    {
        Guard.IsNotNull(config);
        PublicReleaseBranchPatterns = config.Release?.Branches ?? [];
        PrereleaseTag = config.Versioning?.PrereleaseTag;
        AssemblyVersionPrecision = config.Versioning.EffectiveAssemblyVersionPrecision;
    }

    /// <summary>
    /// Gets the regular expressions identifying branches that produce public releases (<c>release.branches</c>).
    /// When empty, no branch produces public releases.
    /// </summary>
    public IReadOnlyList<string> PublicReleaseBranchPatterns { get; }

    /// <summary>
    /// Gets the prerelease tag applied to prerelease versions (<c>versioning.prereleaseTag</c>), or
    /// <see langword="null"/> when unset, in which case prerelease versions are not allowed.
    /// </summary>
    public string? PrereleaseTag { get; }

    /// <summary>
    /// Gets the assembly-version precision (<c>versioning.assemblyVersionPrecision</c>, or
    /// <see cref="VersioningConfig.DefaultAssemblyVersionPrecision"/> when unset).
    /// </summary>
    public AssemblyVersionPrecision AssemblyVersionPrecision { get; }

    /// <summary>
    /// Determines whether releasing from <paramref name="branch"/> produces a public release, by matching it
    /// against the configured <c>release.branches</c> regular expressions.
    /// </summary>
    /// <param name="branch">The short name of the current branch, or the empty string if HEAD is not on a branch.</param>
    /// <returns><see langword="true"/> if <paramref name="branch"/> matches at least one pattern;
    /// otherwise, <see langword="false"/>. The empty string never matches.</returns>
    /// <remarks>
    /// Patterns are implicitly anchored (wrapped in <c>^(?:</c>&#8230;<c>)$</c>): a pattern must match the whole
    /// branch name, so <c>main</c> matches neither <c>domain</c> nor <c>main2</c>.
    /// </remarks>
    /// <exception cref="BuildFailedException">A configured pattern is not a valid regular expression, or matching timed out.</exception>
    public bool IsPublicReleaseBranch(string branch)
    {
        Guard.IsNotNull(branch);
        if (branch.Length == 0)
        {
            return false;
        }

        foreach (var pattern in PublicReleaseBranchPatterns)
        {
            if (IsMatch(pattern, branch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMatch(string pattern, string branch)
    {
        try
        {
            return Regex.IsMatch(
                branch,
                $"^(?:{pattern})$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant,
                RegexMatchTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new BuildFailedException($"Invalid regular expression '{pattern}' in release.branches: {ex.Message}");
        }
        catch (RegexMatchTimeoutException)
        {
            throw new BuildFailedException($"Regular expression '{pattern}' in release.branches timed out matching branch '{branch}'.");
        }
    }
}
