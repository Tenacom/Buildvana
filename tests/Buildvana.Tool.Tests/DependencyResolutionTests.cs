// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;

internal sealed class DependencyResolutionTests
{
    private const string CentralPinFileName = "Directory.Packages.props";

    // A prune run resolves nothing and still answers for every pin, so that a hook deriving state from one
    // particular pin is told about that pin in every run.
    [Test]
    public async Task Skipping_StatesEveryPinOfEveryScopeAsSkipped()
    {
        var resolution = Skipping(Inventory());

        await Assert.That(resolution.Sdks.Select(static sdk => sdk.Pin.Id)).IsEquivalentTo(["Contoso.Sdk"]);
        await Assert.That(resolution.Tools.Select(static tool => tool.Pin.Id)).IsEquivalentTo(["ngbv"]);
        await Assert.That(resolution.Packages.Select(static package => package.Pin.Id)).IsEquivalentTo(["Alpha", "Beta"]);
        var pins = resolution.Sdks.Concat(resolution.Tools).Concat(resolution.Packages);
        await Assert.That(pins.Select(static pin => pin.State).Distinct()).IsEquivalentTo([PinResolutionState.Skipped]);
    }

    // The args state the repository as the run leaves it, and a removed pin is one the repository no longer
    // states. The other scopes hold pins no prune can remove, so they answer in full.
    [Test]
    public async Task Skipping_LeavesOutThePinsTheRunRemoved()
    {
        var inventory = Inventory();
        var removed = inventory.Packages.Where(static pin => pin.Id == "Alpha").ToArray();

        var resolution = Skipping(inventory, removed);

        await Assert.That(resolution.Packages.Select(static package => package.Pin.Id)).IsEquivalentTo(["Beta"]);
        await Assert.That(resolution.Sdks.Count).IsEqualTo(1);
        await Assert.That(resolution.Tools.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Skipping_WithANetSdkPin_StatesItSkippedAndWritesNothing()
    {
        var inventory = Inventory();

        var resolution = Skipping(inventory);

        await Assert.That(resolution.NetSdk!.Pin).IsEqualTo(inventory.NetSdk);
        await Assert.That(resolution.NetSdk.State).IsEqualTo(PinResolutionState.Skipped);
        await Assert.That(resolution.NetSdk.WritesAllowPrerelease).IsFalse();
    }

    [Test]
    public async Task Skipping_WithNoNetSdkPin_StatesNone()
    {
        var resolution = Skipping(new DependencyInventory());

        await Assert.That(resolution.NetSdk).IsNull();
    }

    // Nothing here has fallen behind its policy, because nothing was resolved against a source at all.
    [Test]
    public async Task Skipping_HasNoPendingWork()
    {
        var resolution = Skipping(Inventory());

        await Assert.That(resolution.HasPendingWork).IsFalse();
    }

    // A hook reads the policy governing a pin whatever the run made of the pin itself.
    [Test]
    public async Task Skipping_StatesThePolicyGoverningEachPin()
    {
        var config = new DependenciesConfig
        {
            Policies = [new UpdatePolicyRule { Pattern = "Alpha", Policy = "patch" }],
        };

        var resolution = Skipping(Inventory(), config: config);

        var alpha = resolution.Packages.Single(static package => package.Pin.Id == "Alpha");
        var beta = resolution.Packages.Single(static package => package.Pin.Id == "Beta");
        await Assert.That(alpha.Policy.ToString()).IsEqualTo("patch");
        await Assert.That(beta.Policy.ToString()).IsEqualTo("minor");
    }

    private static DependencyInventory Inventory()
        => new()
        {
            NetSdk = NetSdkPin.Create("10.0.100", allowPrerelease: false),
            Sdks = [Pin(DependencyScope.Sdks, "Contoso.Sdk", "global.json")],
            Tools = [Pin(DependencyScope.Tools, "ngbv", ".config/dotnet-tools.json")],
            Packages =
            [
                Pin(DependencyScope.Packages, "Alpha", CentralPinFileName),
                Pin(DependencyScope.Packages, "Beta", CentralPinFileName),
            ],
        };

    private static DependencyPin Pin(DependencyScope scope, string id, string declaringFile)
        => DependencyPin.Create(scope, id, "1.0.0", declaringFile);

    private static DependencyResolution Skipping(
        DependencyInventory inventory,
        IReadOnlyList<DependencyPin>? removed = null,
        DependenciesConfig? config = null)
    {
        var policies = new EffectivePolicyResolver(config ?? new DependenciesConfig());
        return DependencyResolution.Skipping(inventory, policies, removed ?? []);
    }
}
