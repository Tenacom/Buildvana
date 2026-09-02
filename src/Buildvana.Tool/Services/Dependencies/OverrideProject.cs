// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core.Dependencies;
using Buildvana.Core.HomeDirectory;
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
    /// them and as this run has since left them. It is empty for a project that does not manage its versions
    /// centrally.
    /// </summary>
    public required IReadOnlyDictionary<string, NuGetVersion> CentralPins { get; init; }

    /// <summary>
    /// Folds the evaluations of the solution's projects into one view per project.
    /// </summary>
    /// <param name="home">The home directory, against which a declaring file is named.</param>
    /// <param name="evaluations">The evaluations the pin dump answered with.</param>
    /// <param name="packages">What the run made of the package pins. The evaluations were taken before the
    /// run wrote anything, so a pin it moved is stated there at the version it left behind, and this is what
    /// says where that pin now stands.</param>
    /// <returns>One view per project whose evaluation states where its dependency graph is written. A project
    /// that states none is left out: the Buildvana SDK never reached it, and neither does the lifecycle.</returns>
    public static IReadOnlyList<OverrideProject> Create(
        IHomeDirectoryProvider home,
        IReadOnlyList<PackagePinDump> evaluations,
        IReadOnlyList<PinResolution> packages)
    {
        Guard.IsNotNull(home);
        Guard.IsNotNull(evaluations);
        Guard.IsNotNull(packages);
        var moved = MovedCentralPins(packages);
        return
        [
            .. evaluations
                .Where(static dump => !string.IsNullOrEmpty(dump.ProjectAssetsFile))
                .GroupBy(static dump => dump.ProjectFullPath, StringComparer.OrdinalIgnoreCase)
                .Select(grouped => Fold(home, grouped, moved))
                .OrderBy(static project => project.ProjectFullPath, StringComparer.Ordinal),
        ];
    }

    // Only the central pins are of interest here. A pin is what one file says about one id at one version,
    // and the writer groups by declaring file before it looks one up, so this indexes them the same way: two
    // files may pin one package at one version, and a move of one of them says nothing about the other.
    private static Dictionary<string, Dictionary<PinKey, NuGetVersion>> MovedCentralPins(
        IReadOnlyList<PinResolution> packages)
    {
        var central = PinWriting.Moving(packages)
            .Where(static pin => string.Equals(pin.Pin.ItemType, "PackageVersion", StringComparison.Ordinal))
            .GroupBy(static pin => pin.Pin.DeclaringFile, StringComparer.Ordinal);

        // Folded by hand rather than with ToDictionary: passing TargetsOf there as a method group crashes
        // ReSharper's overload resolution, and inspectcode reports the whole file as unresolvable.
        var moved = new Dictionary<string, Dictionary<PinKey, NuGetVersion>>(StringComparer.Ordinal);
        foreach (var file in central)
        {
            moved[file.Key] = PinWriting.TargetsOf(file);
        }

        return moved;
    }

    // Where two evaluations of one project disagree, the lower of the two answers wins: it is the one that
    // reports more findings and blocks more promotions, and no automatic step should be the bolder reading.
    private static OverrideProject Fold(
        IHomeDirectoryProvider home,
        IGrouping<string, PackagePinDump> evaluations,
        Dictionary<string, Dictionary<PinKey, NuGetVersion>> moved)
    {
        var centralPins = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        var auditLevel = PackageVulnerabilitySeverity.Critical;
        var managesCentrally = false;
        foreach (var evaluation in evaluations)
        {
            managesCentrally |= evaluation.ManagePackageVersionsCentrally;
            auditLevel = (PackageVulnerabilitySeverity)Math.Min((int)auditLevel, (int)AuditLevelOf(evaluation));
            AddCentralPins(home, centralPins, evaluation, moved);
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
        IHomeDirectoryProvider home,
        Dictionary<string, NuGetVersion> pins,
        PackagePinDump evaluation,
        Dictionary<string, Dictionary<PinKey, NuGetVersion>> moved)
    {
        foreach (var item in evaluation.Items)
        {
            if (!string.Equals(item.ItemType, "PackageVersion", StringComparison.Ordinal))
            {
                continue;
            }

            var versionText = EvaluatedMetadata.Stated(item.Version);
            if (versionText is null || !NuGetVersion.TryParse(versionText, out var pinned))
            {
                continue;
            }

            var effective = MovedTo(home, moved, item, versionText) ?? pinned;
            var isLower = !pins.TryGetValue(item.Id, out var known) || VersionComparer.VersionRelease.Compare(effective, known) < 0;
            if (isLower)
            {
                pins[item.Id] = effective;
            }
        }
    }

    // The version a moved pin now states, or null for one this run left where it was. An item declared
    // outside the repository never became a pin, so it never moved either, and it reads as it is stated.
    private static NuGetVersion? MovedTo(
        IHomeDirectoryProvider home,
        Dictionary<string, Dictionary<PinKey, NuGetVersion>> moved,
        PackagePinDumpItem item,
        string versionText)
    {
        if (!home.TryGetRelativePath(item.DefiningProjectFullPath, out var declaringFile))
        {
            return null;
        }

        return moved.TryGetValue(declaringFile, out var targets)
            ? PinWriting.TargetOf(targets, item.ItemType, item.Id, versionText)
            : null;
    }
}
