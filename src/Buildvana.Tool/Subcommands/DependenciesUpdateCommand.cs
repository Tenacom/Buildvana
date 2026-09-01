// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// Moves the repository's dependency pins as far as their policies allow.
/// </summary>
/// <remarks>
/// <para>Every pin of every selected scope is resolved before anything is applied, so that a run either has
/// a target for each pin it manages or changes nothing at all.</para>
/// <para>The command exits 1 when a check run finds pending work. That is the same 1 a failure exits with,
/// and deliberately so: a repository whose pins have fallen behind is a failed check.</para>
/// </remarks>
[ImplementsCommand(
    "dependencies update | deps update",
    settingsType: typeof(DependenciesUpdateSettings),
    usesSdk: true)]
[Description("Update the repository's dependency pins as far as their policies allow.")]
internal sealed class DependenciesUpdateCommand(
    DependenciesUpdateSettings settings,
    BuildvanaConfig config,
    DependencyDiscovery discovery,
    DependencyResolver resolver,
    DependencyApplier applier,
    OverrideLifecycle overrides,
    PostUpdateHookArgsFactory hookArgsFactory,
    HookRunner hookRunner,
    DependencyReportRenderer renderer,
    IReporter reporter) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var selected = DependencyScopeSelection.Resolve(settings.Included, settings.Excluded, config.Dependencies, reporter);
        var scopes = DependencyScopeSelection.Narrow(selected, settings.Filters.Count > 0, settings.To is not null);
        var request = new DependencyResolutionRequest { Filters = settings.Filters, To = settings.To };
        var inventory = await discovery.DiscoverAsync(scopes, cancellationToken).ConfigureAwait(false);
        var resolution = await resolver.ResolveAsync(inventory, request, cancellationToken).ConfigureAwait(false);
        var pending = resolution.HasPendingWork;
        if (!settings.Check)
        {
            await applier.ApplyPinsAsync(resolution, scopes, cancellationToken).ConfigureAwait(false);

            // A check run leaves the lifecycle alone entirely: it predicts pin movement, and predicting what
            // a restore would find is not prediction but a restore.
            if (scopes.Contains(DependencyScope.Packages))
            {
                await overrides.RunAsync(inventory.Evaluations, cancellationToken).ConfigureAwait(false);
            }
        }

        // The hook runs before the baseline is written, so the global.json it sees still states the old .NET
        // SDK version, and its args state the foreseen one.
        var hookArgs = hookArgsFactory.Create(resolution, settings.Check);
        var hookOutcome = await hookRunner
            .RunHookAsync(hookArgs, acceptsPendingWork: settings.Check, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!settings.Check)
        {
            applier.ApplyNetSdk(resolution, scopes);
        }

        renderer.WriteUpdate(resolution, scopes, settings.All, applied: !settings.Check);
        var isStale = pending || hookOutcome == HookOutcome.PendingWork;
        return settings.Check && isStale ? BuildFailedException.DefaultExitCode : 0;
    }
}
