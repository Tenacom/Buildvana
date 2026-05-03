// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Json;
using Buildvana.Core.Process;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Versioning;

/// <summary>
/// Provides methods to manage project versioning.
/// </summary>
public sealed class VersionService
{
    private readonly ILogger<VersionService> _logger;
    private readonly IJsonHelper _jsonHelper;
    private readonly IProcessRunner _processRunner;
    private readonly PublicApiFilesService _publicApiFiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionService"/> class.
    /// </summary>
    public VersionService(
        ILogger<VersionService> logger,
        IJsonHelper jsonHelper,
        IProcessRunner processRunner,
        GitService git,
        PublicApiFilesService publicApiFiles)
    {
        Guard.IsNotNull(logger);
        Guard.IsNotNull(jsonHelper);
        Guard.IsNotNull(processRunner);
        Guard.IsNotNull(git);
        Guard.IsNotNull(publicApiFiles);
        _logger = logger;
        _jsonHelper = jsonHelper;
        _processRunner = processRunner;
        _publicApiFiles = publicApiFiles;
        (CurrentStr, Current, IsPublicRelease, IsPrerelease) = GetVersionInformationFromNbgv();
        (Latest, LatestStable) = git.GetLatestVersions();
    }

    /// <summary>
    /// Gets the version to build, as a string computed by Nerdbank.GitVersioning.
    /// </summary>
    public string CurrentStr { get; private set; }

    /// <summary>
    /// Gets the version to build, as a SemanticVersion object.
    /// </summary>
    public SemanticVersion Current { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a public release can be built.
    /// </summary>
    /// <value>If Git's HEAD is on a public release branch, as indicated in version.json, <see langword="true"/>;
    /// otherwise, <see langword="false"/>.</value>
    public bool IsPublicRelease { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the version to build is a prerelease.
    /// </summary>
    public bool IsPrerelease { get; private set; }

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
        _logger.LogInformation("Current version increment: {Increment}", currentVersionIncrement);

        // Determine the kind of change in public API
        var publicApiChangeKind = checkPublicApiFiles ? _publicApiFiles.GetApiChangeKind() : ApiChangeKind.None;
        _logger.LogInformation(
            "Public API change kind: {Kind}{NotCheckedSuffix}",
            publicApiChangeKind,
            checkPublicApiFiles ? string.Empty : " (not checked)");

        // Determine the version increment required by SemVer rules
        // When the major version is 0, "anything MAY change" according to SemVer;
        // by convention, we increment the minor version for breaking changes (0.x -> 0.(x+1))
        var isMajorVersionZero = LatestStable is { Major: 0 };
        var semanticVersionIncrement = publicApiChangeKind switch {
            ApiChangeKind.Breaking => isMajorVersionZero ? VersionIncrement.Minor : VersionIncrement.Major,
            ApiChangeKind.Additive => isMajorVersionZero ? VersionIncrement.None : VersionIncrement.Minor,
            _ => VersionIncrement.None,
        };
        _logger.LogInformation("Required version increment according to Semantic Versioning rules: {Increment}", semanticVersionIncrement);

        // Determine the requested version increment, if any.
        _logger.LogInformation("Requested version spec change: {Change}", requestedChange);
        var requestedVersionIncrement = requestedChange switch {
            VersionSpecChange.Major => VersionIncrement.Major,
            VersionSpecChange.Minor => VersionIncrement.Minor,
            _ => VersionIncrement.None,
        };
        _logger.LogInformation("Requested version increment: {Increment}.", requestedVersionIncrement);

        // Adjust requested version increment to follow SemVer rules
        if (semanticVersionIncrement > requestedVersionIncrement)
        {
            requestedVersionIncrement = semanticVersionIncrement;
        }

        // Determine the kind of version increment actually required
        var actualVersionIncrement = requestedVersionIncrement > currentVersionIncrement ? requestedVersionIncrement : VersionIncrement.None;
        _logger.LogInformation("Required version increment with respect to current version: {Increment}", actualVersionIncrement);

        // Determine the actual version spec change to apply:
        //   - forget any increment-related change (already accounted for via requestedVersionIncrement)
        //   - set the change to the required increment if any, otherwise leave it as is (None, Unstable, Stable)
        var actualChange = requestedChange switch {
            VersionSpecChange.Major or VersionSpecChange.Minor => VersionSpecChange.None,
            _ => requestedChange,
        };
        actualChange = actualVersionIncrement switch {
            VersionIncrement.Major => VersionSpecChange.Major,
            VersionIncrement.Minor => VersionSpecChange.Minor,
            _ => actualChange,
        };
        _logger.LogInformation("Actual version spec change: {Change}.", actualChange);
        return actualChange;
    }

    /// <summary>
    /// Update version information, typically after a commit.
    /// </summary>
    public void Update() => (CurrentStr, Current, IsPublicRelease, IsPrerelease) = GetVersionInformationFromNbgv();

    private (string CurrentStr, SemanticVersion Current, bool IsPublicRelease, bool IsPrerelease) GetVersionInformationFromNbgv()
    {
        // Block synchronously: this method runs from the constructor (and Update()), neither of which is async.
        var result = _processRunner
            .RunAsync("dotnet", ["nbgv", "get-version", "--format", "json"])
            .GetAwaiter()
            .GetResult();

        var json = _jsonHelper.ParseObject(result.StandardOutput, "The output of nbgv");
        var currentStr = _jsonHelper.GetPropertyValue<string>(json, "SemVer2", "the output of nbgv");
        return (
            currentStr,
            SemanticVersion.Parse(currentStr),
            _jsonHelper.GetPropertyValue<bool>(json, "PublicRelease", "the output of nbgv"),
            !string.IsNullOrEmpty(_jsonHelper.GetPropertyValue<string>(json, "PrereleaseVersion", "the output of nbgv")));
    }
}
