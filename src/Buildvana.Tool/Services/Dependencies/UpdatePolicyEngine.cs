// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.Configuration;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Selects the version a pin may move to under its update policy.
/// </summary>
/// <remarks>
/// <para>The engine is pure: it sees a pin, a candidate set, and a policy, and nothing else. Whether a
/// version exists at all, whether it is listed, and how the effective policy was composed are all the
/// caller's business.</para>
/// <para>A policy is a window anchored at the current pin, not a rule about where the pin is allowed to be:
/// an external edit that moves the anchor is legitimate, and no selection ever lowers a pin.</para>
/// <para>Candidates need not be sorted, and may repeat.</para>
/// <para>Versions compare by precedence, not by text, so that <c>13.0</c> and <c>13.0.0</c> are one version
/// and a pin whose text differs from its target's only in form comes out up to date.</para>
/// </remarks>
internal static class UpdatePolicyEngine
{
    private static readonly TargetSelection DisabledSelection = new() { Outcome = TargetSelectionOutcome.Disabled };

    /// <summary>
    /// Selects the version a package, .NET tool, or MSBuild project SDK pin may move to.
    /// </summary>
    /// <param name="current">The pinned version.</param>
    /// <param name="candidates">The versions the sources know for the pinned package.</param>
    /// <param name="policy">The pin's effective policy.</param>
    /// <returns>The selection.</returns>
    public static TargetSelection Select(
        NuGetVersion current,
        IReadOnlyCollection<NuGetVersion> candidates,
        PackageUpdatePolicy policy)
    {
        Guard.IsNotNull(current);
        Guard.IsNotNull(candidates);
        if (policy.Kind == PackageUpdatePolicyKind.Disable)
        {
            return DisabledSelection;
        }

        List<NuGetVersion> inWindow = [.. candidates.Where(candidate => IsInWindow(current, candidate, policy.Kind))];
        return Compose(current, candidates, inWindow, policy.AllowPrerelease);
    }

    /// <summary>
    /// Selects the version the .NET SDK baseline pin may move to.
    /// </summary>
    /// <param name="current">The pinned version.</param>
    /// <param name="candidates">The releases the .NET release index knows.</param>
    /// <param name="policy">The pin's policy.</param>
    /// <returns>The selection.</returns>
    public static TargetSelection Select(
        NuGetVersion current,
        IReadOnlyCollection<NetSdkRelease> candidates,
        NetSdkUpdatePolicy policy)
    {
        Guard.IsNotNull(current);
        Guard.IsNotNull(candidates);
        if (policy.Kind == NetSdkUpdatePolicyKind.Disable)
        {
            return DisabledSelection;
        }

        // The lts kind removes short-term support releases from the candidate set before resolution, as if
        // they did not exist, and then behaves like major. The latest-stable and latest-preview members still
        // see the whole set: showing what lies beyond the policy is exactly their job, and a new STS release
        // is worth knowing about even under a policy that will never take it.
        var isLtsOnly = policy.Kind == NetSdkUpdatePolicyKind.Lts;
        var eligible = candidates.Where(release => !isLtsOnly || release.IsLts).Select(release => release.Version);
        List<NuGetVersion> inWindow = [.. eligible.Where(candidate => IsInWindow(current, candidate, policy.Kind))];
        List<NuGetVersion> all = [.. candidates.Select(release => release.Version)];
        return Compose(current, all, inWindow, policy.AllowPrerelease);
    }

    // Disable never reaches here: both entry points answer it before composing a window.
    private static bool IsInWindow(NuGetVersion current, NuGetVersion candidate, PackageUpdatePolicyKind kind)
        => kind switch
        {
            PackageUpdatePolicyKind.Exact => IsSameMajorMinor(current, candidate) && candidate.Patch == current.Patch,
            PackageUpdatePolicyKind.Patch => IsSameMajorMinor(current, candidate),
            PackageUpdatePolicyKind.Minor => candidate.Major == current.Major,
            PackageUpdatePolicyKind.Major => true,
            _ => false,
        };

    // Lts widens to everything here, having already narrowed the candidate set to LTS releases.
    private static bool IsInWindow(NuGetVersion current, NuGetVersion candidate, NetSdkUpdatePolicyKind kind)
        => kind switch
        {
            NetSdkUpdatePolicyKind.Patch => IsSameMajorMinor(current, candidate) && FeatureBand(candidate) == FeatureBand(current),
            NetSdkUpdatePolicyKind.Feature => IsSameMajorMinor(current, candidate),
            NetSdkUpdatePolicyKind.Minor => candidate.Major == current.Major,
            NetSdkUpdatePolicyKind.Major or NetSdkUpdatePolicyKind.Lts => true,
            _ => false,
        };

    // The windows key on major, minor, and patch, leaving the revision free: a four-field pin such as
    // 1.2.3.4 may still move to 1.2.3.5 under exact, which is the same major, minor, and patch.
    private static bool IsSameMajorMinor(NuGetVersion a, NuGetVersion b) => a.Major == b.Major && a.Minor == b.Minor;

    // .NET SDK versions are not SemVer: their patch field encodes featureBand * 100 + patch, so that the
    // feature band of 10.0.402 is 4 and its patch is 2.
    private static int FeatureBand(NuGetVersion version) => version.Patch / 100;

    private static TargetSelection Compose(
        NuGetVersion current,
        IReadOnlyCollection<NuGetVersion> candidates,
        IReadOnlyCollection<NuGetVersion> inWindow,
        bool allowPrerelease)
    {
        // Stabilization needs no rule of its own: the stable release of a prerelease pin outranks it by
        // SemVer ordering, and passes a stable-only filter.
        var best = Highest(inWindow.Where(candidate => allowPrerelease || !candidate.IsPrerelease));
        var outcome = Classify(current, best);
        return new TargetSelection
        {
            Outcome = outcome,
            Target = outcome == TargetSelectionOutcome.Update ? best : null,
            LatestStable = Highest(candidates.Where(candidate => !candidate.IsPrerelease)),
            LatestPreview = Highest(candidates.Where(candidate => candidate.IsPrerelease)),
        };
    }

    // A best version below the pin is a problem report, not a downgrade: the pin stays where it is. It
    // happens to a prerelease pin under a stable-only policy with no stable release at or above it, and to an
    // unlisted pin with no listed version above it. The caller flags both; neither is this method's business.
    private static TargetSelectionOutcome Classify(NuGetVersion current, NuGetVersion? best)
    {
        if (best is null)
        {
            return TargetSelectionOutcome.Held;
        }

        var comparison = VersionComparer.VersionRelease.Compare(best, current);
        return comparison > 0 ? TargetSelectionOutcome.Update
            : comparison == 0 ? TargetSelectionOutcome.UpToDate
            : TargetSelectionOutcome.Held;
    }

    // Max returns null for an empty sequence of a reference type, which is exactly "no candidate".
    private static NuGetVersion? Highest(IEnumerable<NuGetVersion> versions)
        => versions.Max<NuGetVersion>(VersionComparer.VersionRelease);
}
