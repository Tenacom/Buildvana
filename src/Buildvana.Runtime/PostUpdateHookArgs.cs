// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// The args handed to the <c>deps/post-update</c> hook: serialized by <c>bv</c> to the hook's args file
/// before the hook runs, deserialized by the hook via <see cref="Load(string)"/>.
/// </summary>
/// <remarks>
/// <para>The hook runs at the end of every <c>bv dependencies</c> run that ran to completion, check runs
/// included, and whether or not anything changed: a hook may have derived state to fix even when no pin
/// moved.</para>
/// <para>The <c>global.json</c> the hook sees still states the old .NET SDK version. <see cref="NetSdk"/>
/// carries the foreseen one, as the release hook is told the version being released before the files carry
/// it.</para>
/// <para>The contract is additive-only: newer <c>bv</c> versions may add properties, never remove or repurpose
/// existing ones. Properties added after the initial release must be optional with a default value, so that an
/// args file written before an update can still be loaded.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record PostUpdateHookArgs : HookArgs, IHookEvent
{
    /// <summary>The name of the context this hook's event belongs to.</summary>
    public const string Context = "deps";

    /// <summary>The name of the event that triggers this hook.</summary>
    public const string Event = "post-update";

    /// <inheritdoc/>
    static string IHookEvent.Context => Context;

    /// <inheritdoc/>
    static string IHookEvent.Event => Event;

    /// <summary>
    /// Gets a value indicating whether the run reports what it would do and changes nothing.
    /// </summary>
    /// <remarks>
    /// <para>A hook of a check run reports what it would change and exits 1 when it would change anything.
    /// The command folds that into its own exit code, as it folds a pin that has fallen behind.</para>
    /// </remarks>
    public required bool Check { get; init; }

    /// <summary>
    /// Gets what the run made of the .NET SDK baseline, or <see langword="null"/> when the scope was not
    /// selected or the repository pins none.
    /// </summary>
    public DependencyResult? NetSdk { get; init; }

    /// <summary>Gets what the run made of the MSBuild project SDK pins.</summary>
    public required IReadOnlyList<DependencyResult> Sdks { get; init; }

    /// <summary>Gets what the run made of the .NET local tool pins.</summary>
    public required IReadOnlyList<DependencyResult> Tools { get; init; }

    /// <summary>
    /// Gets what the run made of the package pins that belong to no additional group.
    /// </summary>
    public required IReadOnlyList<DependencyResult> Packages { get; init; }

    /// <summary>
    /// Gets what the run made of the additional package groups, in configuration order.
    /// </summary>
    public required IReadOnlyList<AdditionalPackagesResult> AdditionalPackages { get; init; }

    /// <summary>
    /// Loads the args of the current hook run from the hook's args file.
    /// </summary>
    /// <param name="homeDirectory">The home directory the args file path is resolved against;
    /// the current directory when omitted (hooks run from the home directory).</param>
    /// <returns>The deserialized hook args.</returns>
    /// <exception cref="BuildvanaRuntimeException">
    /// The args file does not exist (<c>bv</c> has never run this hook in this repository, or the current
    /// directory is not the home directory), cannot be read, or does not contain valid args.
    /// </exception>
    public static PostUpdateHookArgs Load(string? homeDirectory = null)
        => Load(BuildvanaJsonContext.Default.PostUpdateHookArgs, homeDirectory);
}
