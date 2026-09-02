// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Services.Solution;
using CommunityToolkit.Diagnostics;
using NuGet.Common;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Regenerates the transitive override files from the graph the repository actually resolves.
/// </summary>
/// <remarks>
/// <para>The lifecycle runs at the end of every apply run that manages the <c>packages</c> scope. It starts
/// from a restore with the override files left out of the evaluation, which is NuGet's verdict on the graph
/// as the repository states it, and it writes the overrides that verdict calls for. The files are written
/// whole every time, so a stale override needs no pruning: it simply is not written again.</para>
/// <para>Restore says what is vulnerable, and bv does not re-implement the audit. Every NuGet audit setting
/// is therefore honored by construction, on its owner's terms: what a project does not report, the lifecycle
/// does not lift.</para>
/// <para>The whole repository is judged, whatever the invocation named. The files describe the graph, not the
/// command line, so a run aimed at one package can end with a warning about another.</para>
/// </remarks>
internal sealed partial class OverrideLifecycle(
    Lazy<SolutionContext> solution,
    IHomeDirectoryProvider home,
    IDependencyRestorer restorer,
    IVulnerabilityDataSource vulnerabilities,
    IPackageVersionSource versions,
    EffectivePolicyResolver policies,
    SidecarWriter writer,
    IReporter reporter)
{
    // Promotion changes the graph, so the graph is read again after every write until two consecutive reads
    // agree. Ten passes is far past what a real repository needs, and a bound is what turns a mistake in this
    // reasoning into a failed step instead of a command that never returns.
    private const int MaxPasses = 10;

    /// <summary>
    /// Runs the lifecycle over the solution's projects.
    /// </summary>
    /// <param name="evaluations">The evaluations the pin dump answered with.</param>
    /// <param name="packages">What the run made of the package pins, so that a pin the run moved is judged at
    /// the version it moved to and not at the one the evaluations, taken before the run wrote, state.</param>
    /// <param name="cancellationToken">A token that, when signalled, abandons the run.</param>
    /// <returns>A task representing the ongoing operation.</returns>
    /// <exception cref="BuildFailedException">A restore failed for a reason other than its audit findings or
    /// could not read a package source in full, an audit source could not answer, or the graph never
    /// settled.</exception>
    public async Task RunAsync(
        IReadOnlyList<PackagePinDump> evaluations,
        IReadOnlyList<PinResolution> packages,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(evaluations);
        Guard.IsNotNull(packages);
        var projects = OverrideProject.Create(evaluations, packages);
        if (projects.Count == 0)
        {
            reporter.Detail("No project of the solution states where its dependency graph is written, so no override was computed.");
            return;
        }

        reporter.Info("Checking the dependency graph for known vulnerabilities...");
        await RestoreAsync(suppressOverrides: true, cancellationToken).ConfigureAwait(false);
        var assets = Read(projects);
        var state = new RunState();
        for (var pass = 1; pass <= MaxPasses; pass++)
        {
            var verdicts = await DecideAsync(projects, assets, state, cancellationToken).ConfigureAwait(false);
            var changed = writer.Write(state.ToPlan(projects));

            // Nothing written means the graph just read is the one the files on disk produce, which is the
            // stable graph the lifecycle is after. The first pass is the exception: what it read came from a
            // restore with the files suppressed, so a repository that has any must restore once more.
            var isSettled = !changed && (pass > 1 || !state.HasAny);
            if (isSettled)
            {
                Report(verdicts);
                return;
            }

            await RestoreAsync(suppressOverrides: false, cancellationToken).ConfigureAwait(false);
            assets = Read(projects);
        }

        throw new BuildFailedException(
            $"The transitive overrides did not settle in {MaxPasses} passes, so the dependency graph was left as the last pass wrote it.");
    }

    private static bool IsAuditFinding(AssetsLogEntry entry)
        => entry.Code is NuGetLogCode.NU1901 or NuGetLogCode.NU1902 or NuGetLogCode.NU1903 or NuGetLogCode.NU1904;

    // A finding names a package and the target graphs it concerns. An override is a floor and applies to the
    // whole project, so the version to lift from is the highest the flagged graphs resolve.
    private static IEnumerable<(string PackageId, NuGetVersion Resolved)> FindingsOf(ProjectAssets assets)
    {
        var findings = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in assets.Logs.Where(IsAuditFinding))
        {
            foreach (var package in assets.Packages.Where(package => Concerns(entry, package)))
            {
                var isHigher = !findings.TryGetValue(package.Id, out var known)
                    || VersionComparer.VersionRelease.Compare(package.Version, known) > 0;

                if (isHigher)
                {
                    findings[package.Id] = package.Version;
                }
            }
        }

        return findings.Select(static finding => (finding.Key, finding.Value));
    }

    // A log entry that names no target graph concerns the project as a whole.
    private static bool Concerns(AssetsLogEntry entry, ResolvedPackage package)
        => string.Equals(entry.LibraryId, package.Id, StringComparison.OrdinalIgnoreCase)
            && (entry.TargetGraphs.Count == 0 || entry.TargetGraphs.Contains(package.TargetGraph, StringComparer.Ordinal));

    private async Task<List<string>> DecideAsync(
        IReadOnlyList<OverrideProject> projects,
        Dictionary<string, ProjectAssets> assets,
        RunState state,
        CancellationToken cancellationToken)
    {
        var advisories = await vulnerabilities.ReadAsync(cancellationToken).ConfigureAwait(false);
        var verdicts = new List<string>();
        foreach (var project in projects)
        {
            var projectAssets = assets[project.ProjectFullPath];
            foreach (var (packageId, resolved) in FindingsOf(projectAssets))
            {
                var known = await versions.GetVersionsAsync(packageId, cancellationToken).ConfigureAwait(false);
                var request = new OverrideRequest
                {
                    ResolvedVersion = resolved,
                    Candidates = known.Listed,
                    Advisories = advisories.For(packageId),
                    AuditLevel = project.AuditLevel,
                    Policy = policies.ResolveTransitive(packageId),

                    // A reference bv promoted is bv's own, and the graph of a later pass carries it like any
                    // other. Only what the project states itself blocks an override.
                    IsDirectReference = projectAssets.DirectReferences.Contains(packageId, StringComparer.OrdinalIgnoreCase)
                        && !state.HasPromotion(project.ProjectFullPath, packageId),
                    CentralPin = project.CentralPins.GetValueOrDefault(packageId),
                };

                Accept(state, project, packageId, OverrideSelector.Select(request), verdicts, advisories);
            }
        }

        return verdicts;
    }

    private void Accept(
        RunState state,
        OverrideProject project,
        string packageId,
        OverrideDecision decision,
        List<string> verdicts,
        AdvisoryIndex advisories)
    {
        switch (decision.Outcome)
        {
            // A project using central package management takes its version from the central file, so that one
            // version answers for every project that needs it. One managing its own versions takes it inline.
            case OverrideOutcome.Override when project.ManagesVersionsCentrally:
                state.AddCentral(packageId, decision.Version!);
                state.AddPromotion(project.ProjectFullPath, packageId, version: null);
                break;
            case OverrideOutcome.Override:
                state.AddPromotion(project.ProjectFullPath, packageId, decision.Version);
                break;
            case OverrideOutcome.Promote:
                state.AddPromotion(project.ProjectFullPath, packageId, version: null);
                break;
            default:
                verdicts.Add(Verdict(project, packageId, decision.Reason!, advisories));
                break;
        }
    }

    private string Verdict(OverrideProject project, string packageId, string reason, AdvisoryIndex advisories)
    {
        var advisory = advisories.For(packageId).FirstOrDefault(entry => entry.Severity >= project.AuditLevel);
        var where = home.TryGetRelativePath(project.ProjectFullPath, out var relative) ? relative : project.ProjectFullPath;
        var see = advisory is null ? string.Empty : $" See {advisory.Url}.";
        return $"{packageId} is vulnerable in {where}, and no override can lift it: {reason}.{see}";
    }

    private void Report(List<string> verdicts)
    {
        foreach (var verdict in verdicts)
        {
            reporter.Warning(verdict);
        }
    }

    private async Task RestoreAsync(bool suppressOverrides, CancellationToken cancellationToken)
    {
        var exitCode = await restorer
            .RestoreAsync(solution.Value, suppressOverrides, cancellationToken)
            .ConfigureAwait(false);

        // The exit code is not the verdict: a restore whose audit findings are errors fails and still writes
        // every graph. What went wrong, if anything did, is in the graphs themselves.
        reporter.Detail($"The restore exited with code {exitCode}.");
    }

    private Dictionary<string, ProjectAssets> Read(IReadOnlyList<OverrideProject> projects)
    {
        var assets = new Dictionary<string, ProjectAssets>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            assets[project.ProjectFullPath] = ProjectAssetsReader.Read(project.ProjectFullPath, project.AssetsFilePath);
        }

        Judge(assets.Values);
        return assets;
    }

    // What the restore made of itself, read from the graphs it wrote.
    private void Judge(IEnumerable<ProjectAssets> assets)
    {
        foreach (var projectAssets in assets)
        {
            foreach (var entry in projectAssets.Logs)
            {
                Judge(projectAssets, entry);
            }
        }
    }

    private void Judge(ProjectAssets assets, AssetsLogEntry entry)
    {
        // NU1900 says a source could not be read, and says nothing about whether the audit's own data was
        // part of what went missing. A file regenerated from a fraction of the advisories would delete an
        // override that is still needed, so the run stops here and leaves every file as it stands.
        if (entry.Code == NuGetLogCode.NU1900)
        {
            var message = $"The restore of '{assets.ProjectFullPath}' could not read a package source in full, "
                + $"so the overrides were left as they were. {entry.Message}";

            throw new BuildFailedException(ExitCodes.ExternalProgramFailed, message);
        }

        // An audit source with no data at all is NuGet's own warning to make, and it makes it every restore.
        if (entry.Code == NuGetLogCode.NU1905)
        {
            reporter.Warning(entry.Message);
            return;
        }

        if (entry.Level == LogLevel.Error && !IsAuditFinding(entry))
        {
            throw new BuildFailedException(
                ExitCodes.ExternalProgramFailed,
                $"The restore of '{assets.ProjectFullPath}' failed: {entry.Code}: {entry.Message}");
        }
    }
}
