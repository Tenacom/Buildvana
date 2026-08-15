// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using Buildvana.Runtime;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Versioning;

/// <summary>
/// The version being built: the specification it comes from, the repository facts it was computed from,
/// and the version strings a build and a release need.
/// </summary>
/// <remarks>
/// <para>An instance is a snapshot of the repository as it stood when <see cref="VersionCalculator.Calculate"/>
/// produced it. Anything that changes the Git height or the version file makes it stale, so a consumer that
/// outlives such a change calculates again instead of holding on to one.</para>
/// </remarks>
public sealed class VersionInfo
{
    private const int ShortCommitIdLength = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionInfo"/> class, deriving every version string
    /// from the version specification, the repository facts, and the versioning settings.
    /// </summary>
    /// <param name="spec">The version specification read from the version file.</param>
    /// <param name="height">The Git height of the version line, used as the patch number.</param>
    /// <param name="commitId">The SHA of the current <c>HEAD</c> commit, or <see langword="null"/> if the
    /// repository has no commits.</param>
    /// <param name="isPublicRelease"><see langword="true"/> if the current branch produces public releases;
    /// otherwise, <see langword="false"/>.</param>
    /// <param name="prereleaseTag">The prerelease tag to apply, or <see langword="null"/> if the version
    /// is not a prerelease.</param>
    /// <param name="assemblyVersionPrecision">The precision to compute <see cref="AssemblyVersion"/> at.</param>
    internal VersionInfo(
        VersionSpec spec,
        int height,
        string? commitId,
        bool isPublicRelease,
        string? prereleaseTag,
        AssemblyVersionPrecision assemblyVersionPrecision)
    {
        Guard.IsNotNull(spec);
        Spec = spec;
        Height = height;
        CommitId = commitId;
        IsPublicRelease = isPublicRelease;
        SimpleVersion = FormattableString.Invariant($"{spec.Major}.{spec.Minor}.{height}");
        SemVer = prereleaseTag is null ? SimpleVersion : $"{SimpleVersion}-{prereleaseTag}";
        AssemblyVersion = ComputeAssemblyVersion(spec, height, assemblyVersionPrecision);
        FileVersion = FormattableString.Invariant($"{SimpleVersion}.0");
        InformationalVersion = ComputeInformationalVersion(SemVer, prereleaseTag is not null, isPublicRelease, commitId);
    }

    /// <summary>
    /// Gets the version specification read from the version file.
    /// </summary>
    public VersionSpec Spec { get; }

    /// <summary>
    /// Gets the Git height of the version line being built, used as the patch number.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the SHA of the current HEAD commit, or <see langword="null"/> if the repository has no commits.
    /// </summary>
    public string? CommitId { get; }

    /// <summary>
    /// Gets a value indicating whether a public release can be built, i.e. whether the current branch matches
    /// one of the configured <c>release.branches</c> patterns.
    /// </summary>
    public bool IsPublicRelease { get; }

    /// <summary>
    /// Gets a value indicating whether the version being built is a prerelease.
    /// </summary>
    public bool IsPrerelease => Spec.Prerelease;

    /// <summary>
    /// Gets the version in simple <c>MAJOR.MINOR.PATCH</c> form, without any prerelease tag.
    /// </summary>
    public string SimpleVersion { get; }

    /// <summary>
    /// Gets the full semantic version: <see cref="SimpleVersion"/>, plus <c>-tag</c> when the version is a
    /// prerelease. Carries no Git metadata.
    /// </summary>
    public string SemVer { get; }

    /// <summary>
    /// Gets the assembly version: <see cref="VersionSpec.Major"/>, <see cref="VersionSpec.Minor"/>, and
    /// <see cref="Height"/> zeroed out below the configured precision, plus a fourth component that is
    /// always 0.
    /// </summary>
    public string AssemblyVersion { get; }

    /// <summary>
    /// Gets the file version: <see cref="SimpleVersion"/> at full precision, plus a fourth component
    /// that is always 0.
    /// </summary>
    public string FileVersion { get; }

    /// <summary>
    /// Gets the informational version: <see cref="SemVer"/>, plus a <c>g</c>-prefixed short commit ID
    /// appended to the prerelease part when the build is not a public release.
    /// </summary>
    public string InformationalVersion { get; }

    private static string ComputeAssemblyVersion(VersionSpec spec, int height, AssemblyVersionPrecision precision)
    {
        var minor = precision >= AssemblyVersionPrecision.Minor ? spec.Minor : 0;
        var build = precision >= AssemblyVersionPrecision.Build ? height : 0;
        return FormattableString.Invariant($"{spec.Major}.{minor}.{build}.0");
    }

    // Which separator to use is decided by the string being appended to, not by the version spec: a semantic
    // version that already carries a prerelease part takes the commit ID as a further dot-separated identifier,
    // while one that carries none takes it as the prerelease part itself. The prerelease tag is what puts that
    // part there, so reading the answer off the tag keeps the two in step whatever else it is paired with.
    private static string ComputeInformationalVersion(
        string semVer,
        bool hasPrereleaseTag,
        bool isPublicRelease,
        string? commitId)
    {
        if (isPublicRelease || commitId is null)
        {
            return semVer;
        }

        var separator = hasPrereleaseTag ? '.' : '-';
        return FormattableString.Invariant($"{semVer}{separator}g{commitId[..ShortCommitIdLength]}");
    }
}
