// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services.Dependencies;

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
    DependencyReportRenderer renderer,
    IReporter reporter) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var selected = DependencyScopeSelection.Resolve(settings.Included, settings.Excluded, config.Dependencies, reporter);
        var scopes = ScopesOf(selected);
        var request = new DependencyResolutionRequest { Filters = settings.Filters, To = settings.To };
        var inventory = await discovery.DiscoverAsync(scopes, cancellationToken).ConfigureAwait(false);
        var resolution = await resolver.ResolveAsync(inventory, request, cancellationToken).ConfigureAwait(false);
        var pending = resolution.HasPendingWork;
        if (!settings.Check)
        {
            await applier.ApplyPinsAsync(resolution, scopes, cancellationToken).ConfigureAwait(false);
            applier.ApplyNetSdk(resolution, scopes);
        }

        renderer.WriteUpdate(resolution, scopes, settings.All, applied: !settings.Check);
        return settings.Check && pending ? BuildFailedException.DefaultExitCode : 0;
    }

    // An argument names package ids, and the .NET SDK has none: a run that filters by id leaves the baseline
    // alone. Naming the scope outright next to a filter is refused as the contradiction it is.
    // A version stated for the baseline goes the other way: it is about that scope alone.
    private IReadOnlySet<DependencyScope> ScopesOf(IReadOnlySet<DependencyScope> selected)
    {
        if (settings.Filters.Count > 0)
        {
            return selected.Where(static scope => scope != DependencyScope.NetSdk).ToHashSet();
        }

        var isNetSdkEdit = settings.To is not null && selected.Contains(DependencyScope.NetSdk);
        if (isNetSdkEdit && selected.Count > 1)
        {
            throw new BuildFailedException(
                ExitCodes.Usage,
                "--to states the version of the .NET SDK here, so no other scope may be selected. Name a package id, or leave only --netsdk.");
        }

        return selected;
    }
}
