// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Tool.Services.Dependencies;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.SelfUpdate;

/// <summary>
/// Picks the version <c>bv self-update</c> moves the repository to.
/// </summary>
/// <remarks>
/// <para>The reference version is the bv version the tool manifest pins, or the running bv's own version when
/// the manifest has no bv entry. Without an option naming the target, the target is the latest stable version
/// the package sources list at or above the reference. When there is none and the reference is a prerelease,
/// the target is the latest prerelease at or above the reference. Otherwise the target is the reference itself,
/// and the update aligns the other pins to it. A stable pin therefore never moves to a prerelease by itself,
/// and a prerelease pin moves to the first stable version that reaches it, newer prereleases notwithstanding.</para>
/// <para><c>--preview</c> takes the latest listed version at or above the reference, prereleases included.
/// <c>--repair</c> takes the manifest pin and asks no source. <c>--to</c> takes the version given and asks no
/// source either: for those two, the <c>dotnet tool update</c> step of the update is the existence check.</para>
/// <para>Only a listed version is a candidate. A delisted version is often a vulnerable one, and the sources
/// are the one authority on that.</para>
/// </remarks>
internal sealed class SelfUpdateTargetResolver
{
    private const string ToolPackageId = ToolManifest.BvPackageId;

    private readonly IPackageVersionSource _versionSource;
    private readonly NuGetVersion _ownVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfUpdateTargetResolver"/> class.
    /// </summary>
    /// <param name="versionSource">The package sources to ask about bv.</param>
    /// <param name="ownVersion">The version of the running bv.</param>
    public SelfUpdateTargetResolver(IPackageVersionSource versionSource, NuGetVersion ownVersion)
    {
        Guard.IsNotNull(versionSource);
        Guard.IsNotNull(ownVersion);
        _versionSource = versionSource;
        _ownVersion = ownVersion;
    }

    /// <summary>
    /// Picks the target version of an update.
    /// </summary>
    /// <param name="request">What the invocation asks for.</param>
    /// <param name="manifestPin">What the tool manifest says about bv.</param>
    /// <param name="cancellationToken">A token that, when signalled, abandons the lookup.</param>
    /// <returns>The target version, with a clause saying where it came from.</returns>
    /// <exception cref="BuildFailedException"><paramref name="request"/> asks for a repair and the manifest has
    /// no bv entry, no package source is configured, no source knows bv, or a source could not be
    /// reached.</exception>
    public async Task<SelfUpdateTarget> ResolveAsync(
        SelfUpdateRequest request,
        BvManifestPin manifestPin,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(request);
        Guard.IsNotNull(manifestPin);
        if (request.To is { } stated)
        {
            return new SelfUpdateTarget(stated, $"The version given with --to is {stated.ToNormalizedString()}");
        }

        if (request.Repair)
        {
            return manifestPin.Version is { } pinned
                ? new SelfUpdateTarget(pinned, $"The tool manifest pins {ToolPackageId} {pinned.ToNormalizedString()}")
                : throw new BuildFailedException(
                    $"--repair updates the repository to the {ToolPackageId} version the tool manifest pins, "
                    + $"but {ToolManifest.FileName} has no {ToolPackageId} entry. "
                    + $"Run '{ToolPackageId} self-update' without --repair to pin the latest version.");
        }

        var reference = manifestPin.Version ?? _ownVersion;
        var candidates = await ReadCandidatesAsync(reference, cancellationToken).ConfigureAwait(false);
        var best = request.Preview ? Highest(candidates) : HighestByDefault(reference, candidates);
        if (best is not null)
        {
            var qualifier = request.Preview ? ", prereleases included," : string.Empty;
            return new SelfUpdateTarget(
                best,
                $"The latest {ToolPackageId} on the package sources{qualifier} is {best.ToNormalizedString()}");
        }

        var referenceText = reference.ToNormalizedString();
        var origin = manifestPin.Version is null ? "This bv is version" : $"The tool manifest pins {ToolPackageId}";
        return new SelfUpdateTarget(reference, $"{origin} {referenceText}, and the package sources list nothing newer");
    }

    // The default rule: the latest stable version first, and a prerelease only for a prerelease reference
    // that no stable version has reached yet. The candidates are all at or above the reference, so the second
    // pick only ever sees prereleases: a stable among them would have won the first.
    private static NuGetVersion? HighestByDefault(NuGetVersion reference, IReadOnlyList<NuGetVersion> candidates)
    {
        var stable = Highest(candidates.Where(static version => !version.IsPrerelease));
        return stable ?? (reference.IsPrerelease ? Highest(candidates) : null);
    }

    // Max returns null for an empty sequence of a reference type, which is exactly "no candidate".
    private static NuGetVersion? Highest(IEnumerable<NuGetVersion> versions)
        => versions.Max<NuGetVersion>(VersionComparer.VersionRelease);

    // A source that knows nothing of bv is a misconfigured source, not a repository that is up to date:
    // reporting "unchanged" would hide the misconfiguration behind a reassuring summary.
    private async Task<IReadOnlyList<NuGetVersion>> ReadCandidatesAsync(
        NuGetVersion reference,
        CancellationToken cancellationToken)
    {
        BuildFailedException.ThrowIf(
            _versionSource.Sources.Count == 0,
            $"No package source is configured, so {ToolPackageId} has nowhere to look its latest version up.");
        var catalog = await _versionSource.GetVersionsAsync(ToolPackageId, cancellationToken).ConfigureAwait(false);
        BuildFailedException.ThrowIf(
            catalog.Listed.Count == 0 && catalog.Unlisted.Count == 0,
            $"No configured package source knows {ToolPackageId}. Sources: {string.Join(", ", _versionSource.Sources)}.");
        return [.. catalog.Listed.Where(version => VersionComparer.VersionRelease.Compare(version, reference) >= 0)];
    }
}
