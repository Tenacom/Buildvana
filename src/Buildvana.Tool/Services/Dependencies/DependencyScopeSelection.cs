// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Runtime;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Decides which scopes an invocation manages, out of what the configuration file manages and what the
/// command line asks for.
/// </summary>
/// <remarks>
/// <para>Configuration decides first: a scope whose policy is <c>disable</c> is not managed, and no flag
/// brings it back. The command line then restricts what is left, through one of two families of flags:
/// <c>--netsdk</c> and its siblings name the scopes to manage, <c>--no-netsdk</c> and its siblings name the
/// scopes to leave out.</para>
/// <para>The two families do not mix. Asking for one scope and against another in the same invocation
/// states a selection twice, and the two statements can disagree, so it is a usage error rather than a
/// question of precedence.</para>
/// <para>An invocation may well be left with nothing to manage. That is not an error either: the report
/// says what it found, which is nothing.</para>
/// </remarks>
internal static class DependencyScopeSelection
{
    /// <summary>
    /// Resolves the scopes an invocation manages.
    /// </summary>
    /// <param name="included">The scopes the command line names to manage.</param>
    /// <param name="excluded">The scopes the command line names to leave out.</param>
    /// <param name="config">The resolved dependency configuration.</param>
    /// <param name="reporter">The reporter to warn through.</param>
    /// <returns>The scopes to manage.</returns>
    /// <exception cref="BuildFailedException">The command line mixes the two families of flags.</exception>
    public static IReadOnlySet<DependencyScope> Resolve(
        IReadOnlyList<DependencyScope> included,
        IReadOnlyList<DependencyScope> excluded,
        DependenciesConfig config,
        IReporter reporter)
    {
        Guard.IsNotNull(included);
        Guard.IsNotNull(excluded);
        Guard.IsNotNull(config);
        Guard.IsNotNull(reporter);
        if (included.Count > 0 && excluded.Count > 0)
        {
            throw new BuildFailedException(
                ExitCodes.Usage,
                "The options naming the scopes to manage and the options naming the scopes to leave out cannot be mixed.");
        }

        var managed = ManagedScopes(config);
        if (included.Count > 0)
        {
            return ApplyIncluded(included, managed, reporter);
        }

        foreach (var scope in excluded)
        {
            // A scope configuration already disables is one nothing was going to manage: asking for it not
            // to be managed states what is already the case, and needs no warning.
            _ = managed.Remove(scope);
        }

        return managed;
    }

    /// <summary>
    /// Narrows a selection to what the arguments of an update run allow.
    /// </summary>
    /// <param name="selected">The scopes the invocation selected.</param>
    /// <param name="namesPins">Whether an argument names the pins the run is about.</param>
    /// <param name="statesVersion">Whether the command line states a version with <c>--to</c>.</param>
    /// <returns>The scopes to manage.</returns>
    /// <exception cref="BuildFailedException">A version is stated for the .NET SDK baseline while another
    /// scope is selected.</exception>
    /// <remarks>
    /// <para>An argument names package ids, and the .NET SDK has none, so a run that names pins leaves the
    /// baseline alone. Stating a version without naming a pin goes the other way: the version is the
    /// baseline's, and it is an edit of that scope alone.</para>
    /// </remarks>
    public static IReadOnlySet<DependencyScope> Narrow(IReadOnlySet<DependencyScope> selected, bool namesPins, bool statesVersion)
    {
        Guard.IsNotNull(selected);
        if (namesPins)
        {
            return selected.Where(static scope => scope != DependencyScope.NetSdk).ToHashSet();
        }

        const string message = "--to with no argument states the version of the .NET SDK, so that scope must be the only one "
            + "selected. Name a package id, or select the .NET SDK alone with --netsdk.";

        var isNetSdkOnly = selected.Count == 1 && selected.Contains(DependencyScope.NetSdk);
        if (statesVersion && !isNetSdkOnly)
        {
            throw new BuildFailedException(ExitCodes.Usage, message);
        }

        return selected;
    }

    private static HashSet<DependencyScope> ManagedScopes(DependenciesConfig config)
    {
        var scopes = new HashSet<DependencyScope>();
        AddIfManaged(scopes, DependencyScope.Sdks, config.Scopes.Sdks);
        AddIfManaged(scopes, DependencyScope.Tools, config.Scopes.Tools);
        AddIfManaged(scopes, DependencyScope.Packages, config.Scopes.Packages);
        if (NetSdkUpdatePolicy.TryParse(config.Scopes.NetSdk, out var netSdk) && netSdk.Kind != NetSdkUpdatePolicyKind.Disable)
        {
            _ = scopes.Add(DependencyScope.NetSdk);
        }

        return scopes;
    }

    private static void AddIfManaged(HashSet<DependencyScope> scopes, DependencyScope scope, string policy)
    {
        if (PackageUpdatePolicy.TryParse(policy, out var parsed) && parsed.Kind != PackageUpdatePolicyKind.Disable)
        {
            _ = scopes.Add(scope);
        }
    }

    // Asking for a scope the configuration file disables states two things that disagree, and the file is
    // the one that means it: the flag restricts a selection, it does not create one. The warning says so,
    // because a silent no-op would read as a scope with nothing in it.
    private static HashSet<DependencyScope> ApplyIncluded(
        IReadOnlyList<DependencyScope> included,
        HashSet<DependencyScope> managed,
        IReporter reporter)
    {
        var selected = new HashSet<DependencyScope>();
        foreach (var scope in included)
        {
            if (managed.Contains(scope))
            {
                _ = selected.Add(scope);
            }
            else
            {
                reporter.Warning($"The {DependencyScopeNames.Of(scope)} scope is disabled by configuration and is not managed.");
            }
        }

        return selected;
    }
}
