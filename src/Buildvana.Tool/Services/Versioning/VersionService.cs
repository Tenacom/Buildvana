// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Versioning;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Versioning;

/// <summary>
/// Exposes the version being built, computed by <see cref="VersioningService"/>, alongside
/// release-flow version policy: consistency checks against published versions and computation
/// of the version specification change to apply upon release.
/// </summary>
internal sealed partial class VersionService
{
    private readonly IReporter _reporter;
    private readonly IHomeDirectoryProvider _home;
    private readonly VersioningSettings _settings;
    private readonly GitHeightCalculator _heightCalculator;
    private readonly PublicApiFilesService _publicApiFiles;

    private VersioningService _current;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionService"/> class.
    /// </summary>
    public VersionService(
        IReporter reporter,
        IHomeDirectoryProvider home,
        VersioningSettings settings,
        GitService git,
        PublicApiFilesService publicApiFiles)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(home);
        Guard.IsNotNull(settings);
        Guard.IsNotNull(git);
        Guard.IsNotNull(publicApiFiles);
        _reporter = reporter;
        _home = home;
        _settings = settings;
        _publicApiFiles = publicApiFiles;
        _heightCalculator = new(VersionFile.FileName);
        _current = new(reporter, home, settings, _heightCalculator);
        Current = SemanticVersion.Parse(_current.SemVer);
        (Latest, LatestStable) = git.GetLatestVersions();
    }

    /// <summary>
    /// Gets the version to build, as a string.
    /// </summary>
    public string CurrentStr => _current.SemVer;

    /// <summary>
    /// Gets the version to build in simple <c>MAJOR.MINOR.PATCH</c> form, without any prerelease tag.
    /// </summary>
    public string CurrentSimpleStr => _current.SimpleVersion;

    /// <summary>
    /// Gets the version to build, as a SemanticVersion object.
    /// </summary>
    public SemanticVersion Current { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a public release can be built.
    /// </summary>
    /// <value>If Git's HEAD is on a public release branch, as configured in <c>release.branches</c>,
    /// <see langword="true"/>; otherwise, <see langword="false"/>.</value>
    public bool IsPublicRelease => _current.IsPublicRelease;

    /// <summary>
    /// Gets a value indicating whether the version to build is a prerelease.
    /// </summary>
    public bool IsPrerelease => _current.IsPrerelease;

    /// <summary>
    /// Gets the latest published version, if any, as a SemanticVersion object.
    /// </summary>
    public SemanticVersion? Latest { get; }

    /// <summary>
    /// Gets the latest published stable version, if any, as a SemanticVersion object.
    /// </summary>
    public SemanticVersion? LatestStable { get; }

    /// <summary>
    /// Checks the consistency of the current version with respect to latest versions and fails the build if an inconsistency is found.
    /// </summary>
    /// <param name="isFinalCheck"><see langword="true"/> if this is the final check before publishing;
    /// <see langword="false"/> if the current version's patch number might still be incremented,
    /// for example by updating the changelog.</param>
    public void EnsureConsistency(bool isFinalCheck)
    {
        BuildFailedException.ThrowIfNot(
            VersionComparer.Compare(Latest, LatestStable, VersionComparison.Version) >= 0,
            $"Versioning anomaly detected: latest version ({Latest?.ToString() ?? "none"}) is lower than latest stable version ({LatestStable?.ToString() ?? "none"}).");
        if (isFinalCheck)
        {
            BuildFailedException.ThrowIfNot(
                VersionComparer.Compare(Current, LatestStable, VersionComparison.Version) > 0,
                $"Versioning anomaly detected: current version ({Current}) is not higher than latest stable version ({LatestStable?.ToString() ?? "none"}).");
            BuildFailedException.ThrowIfNot(
                VersionComparer.Compare(Current, Latest, VersionComparison.Version) > 0,
                $"Versioning anomaly detected: current version ({Current}) is not higher than latest version ({Latest?.ToString() ?? "none"}).");
        }
        else
        {
            BuildFailedException.ThrowIfNot(
                VersionComparer.Compare(Current, LatestStable, VersionComparison.Version) >= 0,
                $"Versioning anomaly detected: current version ({Current}) is lower than latest stable version ({LatestStable?.ToString() ?? "none"}).");
            BuildFailedException.ThrowIfNot(
                VersionComparer.Compare(Current, Latest, VersionComparison.Version) >= 0,
                $"Versioning anomaly detected: current version ({Current}) is lower than latest version ({Latest?.ToString() ?? "none"}).");
        }
    }

    /// <summary>
    /// Computes the <see cref="VersionSpecChange"/> to apply upon release.
    /// </summary>
    /// <param name="requestedChange">The version spec change requested by the user.</param>
    /// <param name="checkPublicApiFiles">If <see langword="true"/>, account for changes in public API files.</param>
    /// <returns>A newly-created <see cref="VersionSpecChange"/> representing the actual change to apply.</returns>
    public VersionSpecChange ComputeVersionSpecChange(VersionSpecChange requestedChange, bool checkPublicApiFiles)
    {
        // Determine how we are currently already incrementing version
        var currentVersionIncrement = LatestStable == null ? VersionIncrement.None
            : Current.Major > LatestStable.Major ? VersionIncrement.Major
            : Current.Minor > LatestStable.Minor ? VersionIncrement.Minor
            : VersionIncrement.None;
        _reporter.Info($"Current version increment: {currentVersionIncrement}");

        // Determine the kind of change in public API
        var publicApiChangeKind = checkPublicApiFiles ? _publicApiFiles.GetApiChangeKind() : ApiChangeKind.None;
        var notCheckedSuffix = checkPublicApiFiles ? string.Empty : " (not checked)";
        _reporter.Info($"Public API change kind: {publicApiChangeKind}{notCheckedSuffix}");

        // Determine the version increment required by SemVer rules
        // When the major version is 0, "anything MAY change" according to SemVer;
        // by convention, we increment the minor version for breaking changes (0.x -> 0.(x+1))
        var isMajorVersionZero = LatestStable is { Major: 0 };
        var semanticVersionIncrement = publicApiChangeKind switch
        {
            ApiChangeKind.Breaking => isMajorVersionZero ? VersionIncrement.Minor : VersionIncrement.Major,
            ApiChangeKind.Additive => isMajorVersionZero ? VersionIncrement.None : VersionIncrement.Minor,
            _ => VersionIncrement.None,
        };
        _reporter.Info($"Required version increment according to Semantic Versioning rules: {semanticVersionIncrement}");

        // Determine the requested version increment, if any.
        _reporter.Info($"Requested version spec change: {requestedChange}");
        var requestedVersionIncrement = requestedChange switch
        {
            VersionSpecChange.Major => VersionIncrement.Major,
            VersionSpecChange.Minor => VersionIncrement.Minor,
            _ => VersionIncrement.None,
        };
        _reporter.Info($"Requested version increment: {requestedVersionIncrement}.");

        // Adjust requested version increment to follow SemVer rules
        if (semanticVersionIncrement > requestedVersionIncrement)
        {
            requestedVersionIncrement = semanticVersionIncrement;
        }

        // Determine the kind of version increment actually required
        var actualVersionIncrement = requestedVersionIncrement > currentVersionIncrement ? requestedVersionIncrement : VersionIncrement.None;
        _reporter.Info($"Required version increment with respect to current version: {actualVersionIncrement}");

        // Determine the actual version spec change to apply:
        //   - forget any increment-related change (already accounted for via requestedVersionIncrement)
        //   - set the change to the required increment if any, otherwise leave it as is (None, Unstable, Stable)
        var actualChange = requestedChange switch
        {
            VersionSpecChange.Major or VersionSpecChange.Minor => VersionSpecChange.None,
            _ => requestedChange,
        };
        actualChange = actualVersionIncrement switch
        {
            VersionIncrement.Major => VersionSpecChange.Major,
            VersionIncrement.Minor => VersionSpecChange.Minor,
            _ => actualChange,
        };
        _reporter.Info($"Actual version spec change: {actualChange}.");
        return actualChange;
    }

    /// <summary>
    /// Update version information, typically after a commit.
    /// </summary>
    public void Update()
    {
        _current = new(_reporter, _home, _settings, _heightCalculator);
        Current = SemanticVersion.Parse(_current.SemVer);
    }
}
