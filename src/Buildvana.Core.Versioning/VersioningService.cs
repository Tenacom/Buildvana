// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Computes the version being built from the repository's version file, Git height, and versioning settings,
/// and exposes it in the string forms needed by builds and releases.
/// </summary>
/// <remarks>
/// <para>The version file (<see cref="VersionFileName"/>, in the home directory) holds a
/// <c>MAJOR.MINOR[-[tag]]</c> specification; the patch number is the Git height of the version line
/// (see <see cref="GitHeightCalculator"/>). All values are computed once, on construction.</para>
/// </remarks>
public sealed class VersioningService
{
    /// <summary>
    /// The name of the version file, relative to the home directory.
    /// </summary>
    public const string VersionFileName = "VERSION";

    private const int ShortCommitIdLength = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersioningService"/> class, computing the version being built.
    /// </summary>
    /// <param name="reporter">The reporter used to log the computed version.</param>
    /// <param name="home">The home directory provider used to locate the version file and the Git repository.</param>
    /// <param name="settings">The versioning settings.</param>
    /// <param name="heightCalculator">The calculator providing the Git height of the version line.</param>
    /// <exception cref="BuildFailedException">
    /// <para>The version file is absent or does not contain a valid version specification.</para>
    /// <para>The version is a prerelease, but <see cref="VersioningSettings.PrereleaseTag"/> is unset or invalid.</para>
    /// <para>There is no Git repository in the home directory, or a <c>release.branches</c> pattern is invalid.</para>
    /// </exception>
    public VersioningService(
        IReporter reporter,
        IHomeDirectoryProvider home,
        VersioningSettings settings,
        GitHeightCalculator heightCalculator)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(home);
        Guard.IsNotNull(settings);
        Guard.IsNotNull(heightCalculator);

        var homeDirectory = home.HomeDirectory;
        Spec = ReadVersionFile(Path.Combine(homeDirectory, VersionFileName));
        var prereleaseTag = Spec.Prerelease ? GetPrereleaseTag(settings) : null;
        var facts = heightCalculator.Calculate(homeDirectory, Spec);
        Height = facts.Height;
        CommitId = facts.CommitId;
        IsPublicRelease = settings.IsPublicReleaseBranch(facts.BranchName);
        SimpleVersion = FormattableString.Invariant($"{Spec.Major}.{Spec.Minor}.{Height}");
        SemVer = prereleaseTag is null ? SimpleVersion : $"{SimpleVersion}-{prereleaseTag}";
        AssemblyVersion = ComputeAssemblyVersion(Spec, Height, settings.AssemblyVersionPrecision);
        InformationalVersion = ComputeInformationalVersion(SemVer, Spec.Prerelease, IsPublicRelease, CommitId);
        var publicity = IsPublicRelease ? "public release" : "not a public release";
        reporter.Info(FormattableString.Invariant($"Version {SemVer} (height {Height}, {publicity})"));
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
    /// Gets the informational version: <see cref="SemVer"/>, plus a <c>g</c>-prefixed short commit ID
    /// appended to the prerelease part when the build is not a public release.
    /// </summary>
    public string InformationalVersion { get; }

    private static VersionSpec ReadVersionFile(string path)
    {
        BuildFailedException.ThrowIfNot(File.Exists(path), $"Version file {path} not found.");
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new BuildFailedException($"Could not read from {path}: {e.Message}", e);
        }

        BuildFailedException.ThrowIfNot(
            VersionSpec.TryParse(text, out var spec),
            $"{path} contains an invalid version specification '{text.Trim()}'.");
        return spec;
    }

    private static string GetPrereleaseTag(VersioningSettings settings)
    {
        var tag = settings.PrereleaseTag;
        BuildFailedException.ThrowIf(
            string.IsNullOrEmpty(tag),
            $"{VersionFileName} specifies a prerelease version, but versioning.prereleaseTag is not set in the configuration file.");
        var isValid = SemanticVersion.TryParse(FormattableString.Invariant($"0.0.0-{tag}"), out _);
        BuildFailedException.ThrowIfNot(
            isValid,
            $"'{tag}' (versioning.prereleaseTag in the configuration file) is not a valid prerelease tag.");
        return tag;
    }

    private static string ComputeAssemblyVersion(VersionSpec spec, int height, AssemblyVersionPrecision precision)
    {
        var minor = precision >= AssemblyVersionPrecision.Minor ? spec.Minor : 0;
        var build = precision >= AssemblyVersionPrecision.Build ? height : 0;
        return FormattableString.Invariant($"{spec.Major}.{minor}.{build}.0");
    }

    private static string ComputeInformationalVersion(string semVer, bool prerelease, bool isPublicRelease, string? commitId)
    {
        if (isPublicRelease || commitId is null)
        {
            return semVer;
        }

        var separator = prerelease ? '.' : '-';
        return FormattableString.Invariant($"{semVer}{separator}g{commitId[..ShortCommitIdLength]}");
    }
}
