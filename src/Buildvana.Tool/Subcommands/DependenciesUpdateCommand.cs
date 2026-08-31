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
    DependencyReportRenderer renderer,
    IReporter reporter) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var scopes = DependencyScopeSelection.Resolve(settings.Included, settings.Excluded, config.Dependencies, reporter);
        var inventory = await discovery.DiscoverAsync(scopes, cancellationToken).ConfigureAwait(false);
        var resolution = await resolver.ResolveAsync(inventory, cancellationToken).ConfigureAwait(false);
        var pending = resolution.HasPendingWork;
        renderer.WriteUpdate(resolution, scopes, settings.All, applied: false);
        return pending ? BuildFailedException.DefaultExitCode : 0;
    }
}
