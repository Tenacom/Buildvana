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
using Buildvana.Tool.Services.Solution;
using CommunityToolkit.Diagnostics;
using NuGet.Common;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Names the central package pins nothing references any more.
/// </summary>
/// <remarks>
/// <para>The verdict is NuGet's own. A restore runs, and each project's assets file states what that project
/// references directly. A textual scan of the MSBuild files could not answer as much: a reference written
/// through a property or an item transform is one only an evaluation sees.</para>
/// <para>A versionless <c>#:package</c> directive counts as a reference too. It resolves through central
/// package management, so the pin it resolves through is in use.</para>
/// <para>The restore leaves the transitive override files out of the evaluation. Those files hold bv's own
/// references, and a promotion that made a pin look alive would keep it alive for good.</para>
/// </remarks>
internal sealed class OrphanDetector(
    Lazy<SolutionContext> solution,
    IDependencyRestorer restorer,
    IReporter reporter)
{
    /// <summary>
    /// Names the orphaned pins of an inventory.
    /// </summary>
    /// <param name="inventory">What the repository pins.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned restore.</param>
    /// <returns>A task whose result is the pins nothing references, in the order the inventory states
    /// them.</returns>
    /// <exception cref="BuildFailedException">The restore failed for a reason of its own, or wrote no
    /// dependency graph.</exception>
    public async Task<IReadOnlyList<DependencyPin>> DetectAsync(
        DependencyInventory inventory,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(inventory);
        var candidates = inventory.Packages.Where(IsCentralPin).ToArray();
        if (candidates.Length == 0)
        {
            reporter.Detail("The repository states no central package version, so nothing here can be an orphan.");
            return [];
        }

        var projects = ProjectsOf(inventory.Evaluations);
        if (projects.Count == 0)
        {
            reporter.Detail("No project of the solution states where its dependency graph is written, so no reference could be read.");
            return [];
        }

        reporter.Info("Restoring the solution to see which packages its projects reference...");
        var exitCode = await restorer
            .RestoreAsync(solution.Value, suppressTransitiveOverrides: true, cancellationToken)
            .ConfigureAwait(false);

        // The exit code is not the verdict: a restore whose audit findings are errors fails and still writes
        // every graph. What went wrong, if anything did, is in the graphs themselves.
        reporter.Detail($"The restore exited with code {exitCode}.");
        var referenced = new HashSet<string>(inventory.DirectiveReferences, StringComparer.OrdinalIgnoreCase);
        foreach (var (projectFullPath, assetsFilePath) in projects)
        {
            var assets = ProjectAssetsReader.Read(projectFullPath, assetsFilePath);
            EnsureRestored(assets);
            referenced.UnionWith(assets.DirectReferences);
        }

        return [.. candidates.Where(pin => !referenced.Contains(pin.Id))];
    }

    // What a repository pins centrally, whatever file states it: a PackageVersion item is a central pin, and
    // a central pin is the one kind of pin that can be orphaned. A PackageReference is the reference itself,
    // and a directive pin is one too.
    private static bool IsCentralPin(DependencyPin pin)
        => string.Equals(pin.ItemType, "PackageVersion", StringComparison.OrdinalIgnoreCase);

    // A project is evaluated once per target framework, and one assets file answers for all of them.
    private static IReadOnlyList<(string ProjectFullPath, string AssetsFilePath)> ProjectsOf(
        IReadOnlyList<PackagePinDump> evaluations)
        => [.. evaluations
            .Where(static dump => !string.IsNullOrEmpty(dump.ProjectAssetsFile))
            .GroupBy(static dump => dump.ProjectFullPath, StringComparer.OrdinalIgnoreCase)
            .Select(static grouped => (grouped.Key, grouped.First().ProjectAssetsFile!))];

    // Vulnerability data is no concern of orphan detection: what a project references does not depend on it.
    // NU1900, which stops the override lifecycle, therefore does not stop a prune.
    private static void EnsureRestored(ProjectAssets assets)
    {
        foreach (var entry in assets.Logs)
        {
            if (entry.Level == LogLevel.Error && !entry.IsAuditFinding)
            {
                throw new BuildFailedException(
                    ExitCodes.ExternalProgramFailed,
                    $"The restore of '{assets.ProjectFullPath}' failed: {entry.Code}: {entry.Message}");
            }
        }
    }
}
