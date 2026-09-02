// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.Dependencies;
using Buildvana.Tool.Services.Solution;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads everything a repository pins, in the scopes an invocation selected.
/// </summary>
/// <remarks>
/// <para>Every reader answers for one kind of file, and this is where their answers meet. A scope that was
/// not selected costs nothing: the <c>packages</c> scope alone spawns MSBuild, and it does so only when it
/// is selected.</para>
/// <para>Nothing here resolves anything against a package source. What a repository states about itself is
/// readable offline, and <c>bv dependencies show</c> asks for no more than that.</para>
/// </remarks>
internal sealed class DependencyDiscovery(
    Lazy<SolutionContext> solution,
    GlobalJsonPinReader globalJson,
    ToolPinReader tools,
    DirectivePinReader directives,
    SolutionPinReader solutionPins,
    PackagePinReader packages,
    AdditionalGroupPinReader groups)
{
    /// <summary>
    /// Reads the pins of the selected scopes.
    /// </summary>
    /// <param name="scopes">The scopes to read.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates any spawned process.</param>
    /// <returns>What the repository pins.</returns>
    /// <exception cref="BuildFailedException">A file could not be read, or MSBuild could not evaluate the
    /// solution.</exception>
    public async Task<DependencyInventory> DiscoverAsync(
        IReadOnlySet<DependencyScope> scopes,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(scopes);
        var wantsSdks = scopes.Contains(DependencyScope.Sdks);
        var wantsPackages = scopes.Contains(DependencyScope.Packages);
        var globalJsonPins = scopes.Contains(DependencyScope.NetSdk) || wantsSdks ? globalJson.Read() : new GlobalJsonPins(null, []);

        // One scan of the repository's file-based apps answers for both scopes that read directives.
        var directivePins = wantsSdks || wantsPackages ? directives.Read() : DirectivePins.None;
        var packageScope = wantsPackages
            ? await ReadPackagesAsync(directivePins.Pins, cancellationToken).ConfigureAwait(false)
            : ([], []);

        return new DependencyInventory
        {
            NetSdk = scopes.Contains(DependencyScope.NetSdk) ? globalJsonPins.NetSdk : null,
            Sdks = wantsSdks ? [.. globalJsonPins.Sdks, .. OfScope(directivePins.Pins, DependencyScope.Sdks)] : [],
            Tools = scopes.Contains(DependencyScope.Tools) ? tools.Read() : [],
            Packages = packageScope.Pins,
            DirectiveReferences = wantsPackages ? directivePins.References : [],
            Evaluations = packageScope.Evaluations,
        };
    }

    private static IEnumerable<DependencyPin> OfScope(IReadOnlyList<DependencyPin> pins, DependencyScope scope)
        => pins.Where(pin => pin.Scope == scope);

    // An additional group may name a file the solution's own evaluation already reached, and a pin found
    // twice is one pin: the group's policy does not apply to what the packages scope already manages.
    private static IEnumerable<DependencyPin> WithoutDuplicates(
        IReadOnlyList<DependencyPin> groupPins,
        IReadOnlyList<DependencyPin> solutionPins)
    {
        var known = new HashSet<(string DeclaringFile, string Id)>();
        foreach (var pin in solutionPins)
        {
            _ = known.Add((pin.DeclaringFile, pin.Id.ToUpperInvariant()));
        }

        return groupPins.Where(pin => !known.Contains((pin.DeclaringFile, pin.Id.ToUpperInvariant())));
    }

    private async Task<(IReadOnlyList<DependencyPin> Pins, IReadOnlyList<PackagePinDump> Evaluations)> ReadPackagesAsync(
        IReadOnlyList<DependencyPin> directivePins,
        CancellationToken cancellationToken)
    {
        // The solution is read only when the packages scope is selected: a repository with no solution file
        // still has a global.json and a tool manifest, and `bv dependencies --netsdk` must work in it.
        var context = solution.Value;
        var projectPaths = context.Model.SolutionProjects.Select(context.ResolveProjectPath).ToList();
        var dumps = await solutionPins.ReadAsync(projectPaths, cancellationToken).ConfigureAwait(false);
        var solutionPackagePins = packages.Read(dumps);
        var groupPins = await groups.ReadAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DependencyPin> pins =
        [
            .. solutionPackagePins,
            .. WithoutDuplicates(groupPins, solutionPackagePins),
            .. OfScope(directivePins, DependencyScope.Packages),
        ];

        return (pins, dumps);
    }
}
