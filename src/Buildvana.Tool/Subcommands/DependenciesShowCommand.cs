// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure.Execution;
using Buildvana.Tool.Services.Dependencies;

namespace Buildvana.Tool.Subcommands;

/// <summary>
/// Shows what the repository pins and the policy governing each pin.
/// </summary>
/// <remarks>
/// <para>The command works offline: everything it reports, the repository states about itself. The MSBuild
/// evaluation it runs for the <c>packages</c> scope is local work, with the same preconditions as building
/// at all, which is why the command declares that it uses the SDK.</para>
/// <para>It always succeeds. A pin nothing can move, and a pin that disagrees with its own policy, are
/// findings the report states rather than failures: whether to act on them is the reader's call.</para>
/// </remarks>
[ImplementsCommand(
    "dependencies show | dependencies | deps show | deps",
    settingsType: typeof(DependenciesSettings),
    usesSdk: true)]
[Description("Show the repository's dependency pins and the policy governing each.")]
internal sealed class DependenciesShowCommand(
    DependenciesSettings settings,
    BuildvanaConfig config,
    DependencyDiscovery discovery,
    SidecarReader sidecars,
    DependencyReportRenderer renderer,
    IReporter reporter) : IBvCommand
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var scopes = DependencyScopeSelection.Resolve(settings.Included, settings.Excluded, config.Dependencies, reporter);
        var inventory = await discovery.DiscoverAsync(scopes, cancellationToken).ConfigureAwait(false);
        renderer.Write(inventory, scopes);

        // Overrides belong to the packages scope: they are package versions, written for the projects that
        // resolve them. An invocation that leaves that scope out is not asking about them.
        if (scopes.Contains(DependencyScope.Packages))
        {
            renderer.WriteOverrides(sidecars.Read());
        }

        return 0;
    }
}
