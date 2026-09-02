// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.Dependencies;
using CommunityToolkit.Diagnostics;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// One project of the solution, as the transitive override lifecycle needs to see it.
/// </summary>
/// <remarks>
/// <para>A multi-targeting project is evaluated once per target framework, and an override is written per
/// project, so the evaluations of one project are folded into one view of it here.</para>
/// </remarks>
internal sealed record OverrideProject
{
    /// <summary>Gets the full path of the project.</summary>
    public required string ProjectFullPath { get; init; }

    /// <summary>Gets the full path of the file a restore writes the project's dependency graph to.</summary>
    public required string AssetsFilePath { get; init; }

    /// <summary>Gets the severity the project's audit reports from.</summary>
    public required PackageVulnerabilitySeverity AuditLevel { get; init; }

    /// <summary>Gets a value indicating whether the project manages its package versions centrally.</summary>
    public required bool ManagesVersionsCentrally { get; init; }

    /// <summary>
    /// Gets the versions the repository pins centrally, by package id, as this project's evaluations see
    /// them. It is empty for a project that does not manage its versions centrally.
    /// </summary>
    public required IReadOnlyDictionary<string, NuGetVersion> CentralPins { get; init; }

    /// <summary>
    /// Folds the evaluations of the solution's projects into one view per project.
    /// </summary>
    /// <param name="evaluations">The evaluations the pin dump answered with.</param>
    /// <param name="packages">What the run made of the package pins. The evaluations were taken before the
    /// run wrote anything, so a pin it moved is stated there at the version it left behind, and this is what
    /// says where that pin now stands.</param>
    /// <returns>One view per project whose evaluation states where its dependency graph is written. A project
    /// that states none is left out: the Buildvana SDK never reached it, and neither does the lifecycle.</returns>
    public static IReadOnlyList<OverrideProject> Create(
        IReadOnlyList<PackagePinDump> evaluations,
        IReadOnlyList<PinResolution> packages)
    {
        Guard.IsNotNull(evaluations);
        Guard.IsNotNull(packages);
        var moved = MovedCentralPins(packages);
        return
        [
            .. evaluations
                .Where(static dump => !string.IsNullOrEmpty(dump.ProjectAssetsFile))
                .GroupBy(static dump => dump.ProjectFullPath, StringComparer.OrdinalIgnoreCase)
                .Select(grouped => Fold(grouped, moved))
                .OrderBy(static project => project.ProjectFullPath, StringComparer.Ordinal),
        ];
    }

    // Only the central pins are of interest here, and a package pinned twice under two conditions is two
    // pins, each free to move somewhere else. The version a pin left behind is what tells the two apart.
    private static ILookup<string, PinResolution> MovedCentralPins(IReadOnlyList<PinResolution> packages)
        => PinWriting.Moving(packages)
            .Where(static pin => string.Equals(pin.Pin.ItemType, "PackageVersion", StringComparison.Ordinal))
            .ToLookup(static pin => pin.Pin.Id, StringComparer.OrdinalIgnoreCase);

    // Where two evaluations of one project disagree, the lower of the two answers wins: it is the one that
    // reports more findings and blocks more promotions, and no automatic step should be the bolder reading.
    private static OverrideProject Fold(
        IGrouping<string, PackagePinDump> evaluations,
        ILookup<string, PinResolution> moved)
    {
        var centralPins = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        var auditLevel = PackageVulnerabilitySeverity.Critical;
        var managesCentrally = false;
        foreach (var evaluation in evaluations)
        {
            managesCentrally |= evaluation.ManagePackageVersionsCentrally;
            auditLevel = (PackageVulnerabilitySeverity)Math.Min((int)auditLevel, (int)AuditLevelOf(evaluation));
            AddCentralPins(centralPins, evaluation, moved);
        }

        return new OverrideProject
        {
            ProjectFullPath = evaluations.Key,
            AssetsFilePath = evaluations.First().ProjectAssetsFile!,
            AuditLevel = auditLevel,
            ManagesVersionsCentrally = managesCentrally,
            CentralPins = managesCentrally ? centralPins : new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase),
        };
    }

    // NuGet audits from low severity where a project states no level, and it names the levels as its own
    // severity enumeration does. A value that is neither is read as the default, exactly as NuGet reads it.
    private static PackageVulnerabilitySeverity AuditLevelOf(PackagePinDump evaluation)
    {
        var stated = evaluation.NuGetAuditLevel?.Trim() ?? string.Empty;
        var isName = stated.Length > 0 && stated.All(char.IsAsciiLetter);
        return isName && Enum.TryParse<PackageVulnerabilitySeverity>(stated, ignoreCase: true, out var level) && level >= 0
            ? level
            : PackageVulnerabilitySeverity.Low;
    }

    // A pin whose version is not a plain version says nothing this lifecycle can compare, so it is left out
    // and the package is treated as one the repository does not pin.
    private static void AddCentralPins(
        Dictionary<string, NuGetVersion> pins,
        PackagePinDump evaluation,
        ILookup<string, PinResolution> moved)
    {
        foreach (var item in evaluation.Items)
        {
            if (!string.Equals(item.ItemType, "PackageVersion", StringComparison.Ordinal))
            {
                continue;
            }

            if (item.Version is not { } stated || !NuGetVersion.TryParse(stated.Trim(), out var pinned))
            {
                continue;
            }

            var effective = MovedTo(moved, item.Id, pinned) ?? pinned;
            var isLower = !pins.TryGetValue(item.Id, out var known) || VersionComparer.VersionRelease.Compare(effective, known) < 0;
            if (isLower)
            {
                pins[item.Id] = effective;
            }
        }
    }

    // The version a moved pin now states, or null for one this run left where it was.
    private static NuGetVersion? MovedTo(ILookup<string, PinResolution> moved, string id, NuGetVersion stated)
        => moved[id]
            .FirstOrDefault(pin => pin.Pin.Version is { } before && VersionComparer.VersionRelease.Equals(before, stated))
            ?.Target;
}
