// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.Configuration;
using CommunityToolkit.Diagnostics;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Chooses what to do about one vulnerable package of one project.
/// </summary>
/// <remarks>
/// <para>The choice is pure: everything it depends on is in the request, and nothing here reads a file or a
/// package source.</para>
/// <para>An override is a lower bound, so the version chosen is the lowest one that ends the finding, never
/// the furthest the policy would reach. Raising a transitive package further than the advisory requires is a
/// version decision, and version decisions are the repository's.</para>
/// <para>bv never introduces a version for a package the repository pins or references itself. What it may
/// do there is promote: a project that resolves a vulnerable version of a centrally pinned package gets a
/// reference at the pinned version, and no new version appears anywhere.</para>
/// </remarks>
internal static class OverrideSelector
{
    /// <summary>
    /// Chooses what to do about a vulnerable package.
    /// </summary>
    /// <param name="request">What the choice depends on.</param>
    /// <returns>The decision.</returns>
    public static OverrideDecision Select(OverrideRequest request)
    {
        Guard.IsNotNull(request);
        if (request.IsDirectReference)
        {
            return Blocked("the project references it directly, so moving it is the repository's own call");
        }

        var advisories = Applicable(request);
        if (!IsCovered(advisories, request.ResolvedVersion))
        {
            // Restore said the package is vulnerable and the sources' own data does not say which versions
            // are. The two answers come from the same sources, so this means they disagree, and choosing a
            // version from data that does not describe the finding would be a guess.
            return NoFix("no advisory the sources state covers the version the project resolves");
        }

        return request.CentralPin is { } pin
            ? SelectForCentralPin(request, advisories, pin)
            : SelectVersion(request, advisories);
    }

    // NuGet's audit reports an advisory whose severity is at or above the project's audit level, and its
    // severity for an advisory it cannot rank is Unknown, which sorts below every level. The lifecycle reads
    // the same data the same way, so that it never lifts a graph out of a finding restore does not report.
    private static List<PackageAdvisory> Applicable(OverrideRequest request)
        => [.. request.Advisories.Where(advisory => advisory.Severity >= request.AuditLevel)];

    private static bool IsCovered(List<PackageAdvisory> advisories, NuGetVersion version)
        => advisories.Exists(advisory => advisory.AffectedVersions.Satisfies(version));

    private static OverrideDecision SelectForCentralPin(OverrideRequest request, List<PackageAdvisory> advisories, NuGetVersion pin)
    {
        if (VersionComparer.VersionRelease.Compare(pin, request.ResolvedVersion) < 0)
        {
            return Blocked(
                $"the repository pins it at {pin.ToNormalizedString()}, below the {request.ResolvedVersion.ToNormalizedString()} "
                + "this project resolves, and a promotion would downgrade the project");
        }

        return IsCovered(advisories, pin)
            ? Blocked($"the repository pins it at {pin.ToNormalizedString()}, which an advisory covers")
            : new OverrideDecision(OverrideOutcome.Promote, null, null);
    }

    private static OverrideDecision SelectVersion(OverrideRequest request, List<PackageAdvisory> advisories)
    {
        List<NuGetVersion> safe = [.. request.Candidates
            .Where(candidate => VersionComparer.VersionRelease.Compare(candidate, request.ResolvedVersion) >= 0)
            .Where(candidate => !IsCovered(advisories, candidate))
            .OrderBy(static candidate => candidate, VersionComparer.VersionRelease)];

        if (safe.Count == 0)
        {
            return NoFix("no version the sources list is outside every advisory");
        }

        var target = safe.Find(candidate => UpdatePolicyEngine.Allows(request.ResolvedVersion, candidate, request.Policy));
        return target is not null
            ? new OverrideDecision(OverrideOutcome.Override, target, null)
            : NoFix(WhyNoVersionWillDo(request, safe));
    }

    private static string WhyNoVersionWillDo(OverrideRequest request, List<NuGetVersion> safe)
    {
        if (request.Policy.Kind == PackageUpdatePolicyKind.Disable)
        {
            return "its policy disables updates";
        }

        List<NuGetVersion> allowedForms = [.. safe.Where(candidate => request.Policy.AllowPrerelease || !candidate.IsPrerelease)];
        return allowedForms.Count == 0
            ? $"every version outside every advisory is a prerelease, and its '{request.Policy}' policy takes stable versions only"
            : $"the lowest version outside every advisory is {allowedForms[0].ToNormalizedString()}, "
                + $"beyond what its '{request.Policy}' policy allows";
    }

    private static OverrideDecision Blocked(string reason) => new(OverrideOutcome.Blocked, null, reason);

    private static OverrideDecision NoFix(string reason) => new(OverrideOutcome.NoFix, null, reason);
}
