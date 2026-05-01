// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core;
using Buildvana.Core.Json;
using Buildvana.Tool.Services.Git;
using Buildvana.Tool.Services.PublicApiFiles;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Tool;
using Cake.Core;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Versioning;

/// <summary>
/// Provides methods to manage project versioning.
/// </summary>
public sealed class VersionService
{
    private readonly ICakeContext _context;
    private readonly IBuildHost _host;
    private readonly PublicApiFilesService _publicApiFiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionService"/> class.
    /// </summary>
    public VersionService(ICakeContext context, IBuildHost host, GitService git, PublicApiFilesService publicApiFiles)
    {
        Guard.IsNotNull(context);
        Guard.IsNotNull(host);
        Guard.IsNotNull(git);
        Guard.IsNotNull(publicApiFiles);
        _context = context;
        _host = host;
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
        _host.Ensure(
            VersionComparer.Compare(Latest, LatestStable, VersionComparison.Version) >= 0,
            $"Versioning anomaly detected: latest version ({Latest?.ToString() ?? "none"}) is lower than latest stable version ({LatestStable?.ToString() ?? "none"}).");
        if (isFinalCheck)
        {
            _host.Ensure(
                VersionComparer.Compare(Current, LatestStable, VersionComparison.Version) > 0,
                $"Versioning anomaly detected: current version ({Current}) is not higher than latest stable version ({LatestStable?.ToString() ?? "none"}).");
            _host.Ensure(
                VersionComparer.Compare(Current, Latest, VersionComparison.Version) > 0,
                $"Versioning anomaly detected: current version ({Current}) is not higher than latest version ({Latest?.ToString() ?? "none"}).");
        }
        else
        {
            _host.Ensure(
                VersionComparer.Compare(Current, LatestStable, VersionComparison.Version) >= 0,
                $"Versioning anomaly detected: current version ({Current}) is lower than latest stable version ({LatestStable?.ToString() ?? "none"}).");
            _host.Ensure(
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
        _host.LogInformation($"Current version increment: {currentVersionIncrement}");

        // Determine the kind of change in public API
        var publicApiChangeKind = checkPublicApiFiles ? _publicApiFiles.GetApiChangeKind() : ApiChangeKind.None;
        _host.LogInformation($"Public API change kind: {publicApiChangeKind}{(checkPublicApiFiles ? null : " (not checked)")}");

        // Determine the version increment required by SemVer rules
        // When the major version is 0, "anything MAY change" according to SemVer;
        // by convention, we increment the minor version for breaking changes (0.x -> 0.(x+1))
        var isMajorVersionZero = LatestStable is { Major: 0 };
        var semanticVersionIncrement = publicApiChangeKind switch {
            ApiChangeKind.Breaking => isMajorVersionZero ? VersionIncrement.Minor : VersionIncrement.Major,
            ApiChangeKind.Additive => isMajorVersionZero ? VersionIncrement.None : VersionIncrement.Minor,
            _ => VersionIncrement.None,
        };
        _host.LogInformation($"Required version increment according to Semantic Versioning rules: {semanticVersionIncrement}");

        // Determine the requested version increment, if any.
        _host.LogInformation($"Requested version spec change: {requestedChange}");
        var requestedVersionIncrement = requestedChange switch {
            VersionSpecChange.Major => VersionIncrement.Major,
            VersionSpecChange.Minor => VersionIncrement.Minor,
            _ => VersionIncrement.None,
        };
        _host.LogInformation($"Requested version increment: {requestedVersionIncrement}.");

        // Adjust requested version increment to follow SemVer rules
        if (semanticVersionIncrement > requestedVersionIncrement)
        {
            requestedVersionIncrement = semanticVersionIncrement;
        }

        // Determine the kind of version increment actually required
        var actualVersionIncrement = requestedVersionIncrement > currentVersionIncrement ? requestedVersionIncrement : VersionIncrement.None;
        _host.LogInformation($"Required version increment with respect to current version: {actualVersionIncrement}");

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
        _host.LogInformation($"Actual version spec change: {actualChange}.");
        return actualChange;
    }

    /// <summary>
    /// Update version information, typically after a commit.
    /// </summary>
    public void Update() => (CurrentStr, Current, IsPublicRelease, IsPrerelease) = GetVersionInformationFromNbgv();

    private (string CurrentStr, SemanticVersion Current, bool IsPublicRelease, bool IsPrerelease) GetVersionInformationFromNbgv()
    {
        var nbgvOutput = new StringBuilder();
        _context.DotNetTool(
            null,
            "nbgv",
            new ProcessArgumentBuilder()
                .Append("get-version")
                .Append("--format")
                .Append("json"),
            new DotNetToolSettings
            {
                SetupProcessSettings = s => s
                    .SetRedirectStandardOutput(true)
                    .SetRedirectedStandardOutputHandler(x =>
                    {
                        _ = nbgvOutput.AppendLine(x);
                        return x;
                    }),
            });

        var json = _host.ParseJsonObject(nbgvOutput.ToString(), "The output of nbgv");
        var currentStr = _host.GetJsonPropertyValue<string>(json, "SemVer2", "the output of nbgv");
        return (
            currentStr,
            SemanticVersion.Parse(currentStr),
            _host.GetJsonPropertyValue<bool>(json, "PublicRelease", "the output of nbgv"),
            !string.IsNullOrEmpty(_host.GetJsonPropertyValue<string>(json, "PrereleaseVersion", "the output of nbgv")));
    }
}
