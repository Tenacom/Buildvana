// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Services.Hooks;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Removes the central package pins nothing references any more.
/// </summary>
/// <remarks>
/// <para>Removing an entry and moving a version forward are different verbs with different consequences,
/// which is why this is a command of its own rather than an option of <c>dependencies update</c>.</para>
/// <para>Orphans exist among central package pins alone, so every other scope is left alone here. The scopes
/// are still read, because the hook answers for every pin of every selected scope.</para>
/// <para>Policy plays no part: a pin whose policy is <c>disable</c> is removed like any other. A policy says
/// how far a pin may move, and says nothing about whether the repository still needs it.</para>
/// </remarks>
[ImplementsCommand(
    "dependencies prune | deps prune",
    settingsType: typeof(DependenciesPruneSettings),
    usesSdk: true)]
[Description("Remove the NuGet package pins nothing references any more.")]
internal sealed class DependenciesPruneCommand(
    DependenciesPruneSettings settings,
    BuildvanaConfig config,
    DependencyDiscovery discovery,
    OrphanDetector detector,
    PackagePinWriter writer,
    OverrideLifecycle overrides,
    EffectivePolicyResolver policies,
    PostUpdateHookArgsFactory hookArgsFactory,
    HookRunner hookRunner,
    DependencyReportRenderer renderer,
    IReporter reporter) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var scopes = DependencyScopeSelection.Resolve(settings.Included, settings.Excluded, config.Dependencies, reporter);
        if (!scopes.Contains(DependencyScope.Packages))
        {
            reporter.Info("Only the packages scope can hold an orphaned pin, and this run leaves it out.");
            return 0;
        }

        var inventory = await discovery.DiscoverAsync(scopes, cancellationToken).ConfigureAwait(false);
        var orphans = await detector.DetectAsync(inventory, cancellationToken).ConfigureAwait(false);
        if (orphans.Count > 0 && !settings.Check)
        {
            writer.Remove(orphans);

            // A promotion may depend on a pin this run has just removed, and a reference with no version to
            // resolve is one the next restore rejects. The lifecycle is entered where a run with no pin
            // update enters it: at the restore.
            await overrides.RunAsync(inventory.Evaluations, packages: [], orphans, cancellationToken).ConfigureAwait(false);
        }

        // The args state the repository as the run leaves it, so a pin the run removed is gone from them. A
        // check run removed none, and states them all.
        IReadOnlyList<DependencyPin> removed = settings.Check ? [] : orphans;
        var hookArgs = hookArgsFactory.Create(DependencyResolution.Skipping(inventory, policies, removed), settings.Check);
        var hookOutcome = await hookRunner
            .RunHookAsync(hookArgs, acceptsPendingWork: settings.Check, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        renderer.WritePrune(orphans, removed: !settings.Check);
        var isStale = orphans.Count > 0 || hookOutcome == HookOutcome.PendingWork;
        return settings.Check && isStale ? BuildFailedException.DefaultExitCode : 0;
    }
}
