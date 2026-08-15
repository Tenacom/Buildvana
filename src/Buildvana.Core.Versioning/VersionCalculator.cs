// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Core.HomeDirectory;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Computes the version being built from the repository's version file, Git height, and versioning settings.
/// </summary>
/// <remarks>
/// <para>The version file (<see cref="VersionFile.FileName"/>, in the home directory) holds a
/// <c>MAJOR.MINOR[-[tag]]</c> specification; the patch number is the Git height of the version line
/// (see <see cref="GitHeightCalculator"/>).</para>
/// <para>An instance carries no computed state: it reads the repository afresh on every
/// <see cref="Calculate"/> call, so a consumer that changes what the version depends on — by committing,
/// or by rewriting the version file — calls it again for a <see cref="VersionInfo"/> matching the new state.</para>
/// </remarks>
public sealed class VersionCalculator
{
    private readonly IHomeDirectoryProvider _home;
    private readonly VersioningSettings _settings;
    private readonly GitHeightCalculator _heightCalculator;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionCalculator"/> class.
    /// </summary>
    /// <param name="home">The home directory provider used to locate the version file and the Git repository.</param>
    /// <param name="settings">The versioning settings.</param>
    /// <param name="heightCalculator">The calculator providing the Git height of the version line.</param>
    public VersionCalculator(IHomeDirectoryProvider home, VersioningSettings settings, GitHeightCalculator heightCalculator)
    {
        Guard.IsNotNull(home);
        Guard.IsNotNull(settings);
        Guard.IsNotNull(heightCalculator);
        _home = home;
        _settings = settings;
        _heightCalculator = heightCalculator;
    }

    /// <summary>
    /// Computes the version being built, as the repository stands now.
    /// </summary>
    /// <returns>A newly-created <see cref="VersionInfo"/>.</returns>
    /// <exception cref="BuildFailedException">
    /// <para>The version file is absent or does not contain a valid version specification.</para>
    /// <para>The version is a prerelease, but <see cref="VersioningSettings.PrereleaseTag"/> is unset or invalid.</para>
    /// <para>There is no Git repository in the home directory, or a <c>release.branches</c> pattern is invalid.</para>
    /// </exception>
    public VersionInfo Calculate()
    {
        var spec = VersionFile.Load(_home).Spec;
        var prereleaseTag = spec.Prerelease ? GetPrereleaseTag(_settings) : null;
        var facts = _heightCalculator.Calculate(_home.HomeDirectory, spec);
        return new(
            spec,
            facts.Height,
            facts.CommitId,
            _settings.IsPublicReleaseBranch(facts.BranchName),
            prereleaseTag,
            _settings.AssemblyVersionPrecision);
    }

    private static string GetPrereleaseTag(VersioningSettings settings)
    {
        var tag = settings.PrereleaseTag;
        BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(tag),
            $"{VersionFile.FileName} specifies a prerelease version, but versioning.prereleaseTag is not set in the configuration file.");
        var isValid = SemanticVersion.TryParse(FormattableString.Invariant($"0.0.0-{tag}"), out _);
        BuildFailedException.ThrowIfNot(
            isValid,
            $"'{tag}' (versioning.prereleaseTag in the configuration file) is not a valid prerelease tag.");
        return tag;
    }
}
