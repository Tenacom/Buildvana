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
/// <para>The candidate set must contain the pinned version whenever the source lists it. An up-to-date pin
/// is recognized by finding itself among the candidates, so a caller that pre-filters candidates to versions
/// above the pin would report every up-to-date pin as held.</para>
/// <para>Versions compare by precedence, not by text, so that <c>13.0</c> and <c>13.0.0</c> are one version
/// and a pin whose text differs from its target's only in form comes out up to date.</para>
/// </remarks>
internal static class UpdatePolicyEngine
{
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
            return ComposeDisabled(candidates);
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
            return ComposeDisabled([.. candidates.Select(release => release.Version)]);
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

    /// <summary>
    /// Says whether a policy lets a package at one version move to another.
    /// </summary>
    /// <param name="current">The version to move from.</param>
    /// <param name="candidate">The version to move to.</param>
    /// <param name="policy">The effective policy.</param>
    /// <returns><see langword="true"/> if the move is within the policy, <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// <para>This is the window the two <c>Select</c> overloads pick a target inside, asked about one version.
    /// The transitive override lifecycle needs the lowest version that ends a vulnerability rather than the
    /// furthest one a pin may reach, and the window it must stay inside is the same window.</para>
    /// </remarks>
    public static bool Allows(NuGetVersion current, NuGetVersion candidate, PackageUpdatePolicy policy)
    {
        Guard.IsNotNull(current);
        Guard.IsNotNull(candidate);
        var isAllowedForm = policy.AllowPrerelease || !candidate.IsPrerelease;
        return policy.Kind != PackageUpdatePolicyKind.Disable
            && isAllowedForm
            && IsInWindow(current, candidate, policy.Kind);
    }

    // Disable resolves no target, but the latest-version members still report whatever the caller resolved:
    // a pin the user froze is where "what is out there" is most worth reading. A caller that resolves nothing
    // for a disabled pin passes an empty candidate set and gets nulls back.
    private static TargetSelection ComposeDisabled(IReadOnlyCollection<NuGetVersion> candidates)
        => CreateSelection(TargetSelectionOutcome.Disabled, target: null, candidates);

    // Disable never reaches here: both entry points answer it before composing a window.
    // NuGetVersion.Revision is the fourth field of x.y.z.R, and reads zero on a version that has none, so a
    // three-field pin and a three-field candidate compare equal there.
    private static bool IsInWindow(NuGetVersion current, NuGetVersion candidate, PackageUpdatePolicyKind kind)
        => kind switch
        {
            PackageUpdatePolicyKind.Exact => IsSameMajorMinorPatch(current, candidate) && candidate.Revision == current.Revision,
            PackageUpdatePolicyKind.Revision => IsSameMajorMinorPatch(current, candidate),
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

    private static bool IsSameMajorMinorPatch(NuGetVersion a, NuGetVersion b)
        => IsSameMajorMinor(a, b) && a.Patch == b.Patch;

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
        return CreateSelection(outcome, outcome == TargetSelectionOutcome.Update ? best : null, candidates);
    }

    private static TargetSelection CreateSelection(
        TargetSelectionOutcome outcome,
        NuGetVersion? target,
        IReadOnlyCollection<NuGetVersion> candidates)
        => new()
        {
            Outcome = outcome,
            Target = target,
            LatestStable = Highest(candidates.Where(candidate => !candidate.IsPrerelease)),
            LatestPreview = Highest(candidates.Where(candidate => candidate.IsPrerelease)),
        };

    // Max returns null for an empty sequence of a reference type, which is exactly "no candidate".
    private static NuGetVersion? Highest(IEnumerable<NuGetVersion> versions)
        => versions.Max<NuGetVersion>(VersionComparer.VersionRelease);

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
}
