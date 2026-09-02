// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What a run made of everything the repository pins in the selected scopes: the offline inventory, answered
/// against the package sources and the .NET release index.
/// </summary>
/// <remarks>
/// <para>A scope that was not selected contributes nothing here, which is not the same as a scope that has
/// no pin.</para>
/// </remarks>
internal sealed record DependencyResolution
{
    /// <summary>
    /// Gets what the run made of the .NET SDK baseline, or <see langword="null"/> when the scope was not
    /// selected or the repository pins none.
    /// </summary>
    public NetSdkResolution? NetSdk { get; init; }

    /// <summary>Gets what the run made of the MSBuild project SDK pins.</summary>
    public IReadOnlyList<PinResolution> Sdks { get; init; } = [];

    /// <summary>Gets what the run made of the .NET local tool pins.</summary>
    public IReadOnlyList<PinResolution> Tools { get; init; } = [];

    /// <summary>Gets what the run made of the package pins, additional groups included.</summary>
    public IReadOnlyList<PinResolution> Packages { get; init; } = [];

    /// <summary>
    /// Gets whether anything in the selected scopes is not where its policy would put it: a pin with a
    /// target, or a <c>global.json</c> whose <c>allowPrerelease</c> disagrees with the policy. This is the
    /// verdict of a check run.
    /// </summary>
    public bool HasPendingWork
        => NetSdk is { State: PinResolutionState.Updated } or { WritesAllowPrerelease: true }
            || Sdks.Concat(Tools).Concat(Packages).Any(static pin => pin.State == PinResolutionState.Updated);

    /// <summary>
    /// States every pin of an inventory as skipped, which is what a run that resolves none of them made of
    /// them.
    /// </summary>
    /// <param name="inventory">What the repository pins.</param>
    /// <param name="policies">What composes the policy governing each pin.</param>
    /// <param name="removed">The pins the run removed, which the repository no longer states.</param>
    /// <returns>The resolution.</returns>
    /// <remarks>
    /// <para><c>bv dependencies prune</c> resolves nothing and still answers for every pin, so that a hook
    /// deriving state from a particular pin is told about that pin in every run.</para>
    /// </remarks>
    public static DependencyResolution Skipping(
        DependencyInventory inventory,
        EffectivePolicyResolver policies,
        IReadOnlyList<DependencyPin> removed)
    {
        Guard.IsNotNull(inventory);
        Guard.IsNotNull(policies);
        Guard.IsNotNull(removed);
        var gone = new HashSet<DependencyPin>(removed);
        return new DependencyResolution
        {
            NetSdk = inventory.NetSdk is { } netSdk ? Skipping(netSdk, policies) : null,
            Sdks = Skipping(inventory.Sdks, policies),
            Tools = Skipping(inventory.Tools, policies),
            Packages = Skipping(inventory.Packages.Where(pin => !gone.Contains(pin)), policies),
        };
    }

    private static NetSdkResolution Skipping(NetSdkPin pin, EffectivePolicyResolver policies)
        => new()
        {
            Pin = pin,
            Policy = policies.ResolveNetSdk(),
            State = PinResolutionState.Skipped,
            WritesAllowPrerelease = false,
        };

    private static IReadOnlyList<PinResolution> Skipping(
        IEnumerable<DependencyPin> pins,
        EffectivePolicyResolver policies)
        => [.. pins.Select(pin => new PinResolution
        {
            Pin = pin,
            Policy = policies.Resolve(pin),
            State = PinResolutionState.Skipped,
        })];
}
