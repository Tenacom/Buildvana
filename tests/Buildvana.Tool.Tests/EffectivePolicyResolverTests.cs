// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;

internal sealed class EffectivePolicyResolverTests
{
    [Test]
    public async Task Resolve_WithNothingStated_TakesTheScopeDefault()
    {
        var resolver = new EffectivePolicyResolver(new DependenciesConfig());
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Packages))).IsEqualTo("minor");
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Sdks))).IsEqualTo("minor");
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Tools))).IsEqualTo("minor");
        await Assert.That(resolver.ResolveNetSdk().ToString()).IsEqualTo("major");
    }

    [Test]
    public async Task Resolve_TakesTheScopeOfThePin()
    {
        var config = new DependenciesConfig
        {
            Scopes = new DependencyScopesConfig { NetSdk = "lts", Sdks = "patch", Tools = "major-", Packages = "exact" },
        };

        var resolver = new EffectivePolicyResolver(config);
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Packages))).IsEqualTo("exact");
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Sdks))).IsEqualTo("patch");
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Tools))).IsEqualTo("major-");
        await Assert.That(resolver.ResolveNetSdk().ToString()).IsEqualTo("lts");
    }

    [Test]
    public async Task Resolve_TakesTheFirstMatchingPattern()
    {
        var config = new DependenciesConfig
        {
            Policies =
            [
                new UpdatePolicyRule { Pattern = "Serilog", Policy = "patch" },
                new UpdatePolicyRule { Pattern = "Serilog*", Policy = "major" },
            ],
        };

        var resolver = new EffectivePolicyResolver(config);
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Packages))).IsEqualTo("patch");
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Packages, "Serilog.Sinks.Console"))).IsEqualTo("major");
    }

    // The patterns govern every id-shaped scope, not the packages one alone.
    [Test]
    public async Task Resolve_AppliesPatternsToEveryIdShapedScope()
    {
        var config = new DependenciesConfig
        {
            Policies = [new UpdatePolicyRule { Pattern = "*", Policy = "patch-" }],
        };

        var resolver = new EffectivePolicyResolver(config);
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Tools))).IsEqualTo("patch-");
        await Assert.That(Resolve(resolver, Pin(DependencyScope.Sdks))).IsEqualTo("patch-");
    }

    [Test]
    public async Task Resolve_OfAGroupPin_TakesTheGroupPolicy()
    {
        var resolver = new EffectivePolicyResolver(GroupConfig("revision"));
        var pin = Pin(DependencyScope.Packages) with { GroupCaption = "SDK package injections" };
        await Assert.That(Resolve(resolver, pin)).IsEqualTo("revision");
    }

    // A pattern outranks the policy of the group a pin belongs to.
    [Test]
    public async Task Resolve_OfAGroupPinMatchedByAPattern_TakesThePattern()
    {
        var config = GroupConfig("revision") with
        {
            Policies = [new UpdatePolicyRule { Pattern = "Serilog", Policy = "exact" }],
        };

        var resolver = new EffectivePolicyResolver(config);
        var pin = Pin(DependencyScope.Packages) with { GroupCaption = "SDK package injections" };
        await Assert.That(Resolve(resolver, pin)).IsEqualTo("exact");
    }

    // The pin's own metadata outranks everything.
    [Test]
    public async Task Resolve_OfAPinStatingItsOwnPolicy_TakesIt()
    {
        var config = GroupConfig("revision") with
        {
            Policies = [new UpdatePolicyRule { Pattern = "*", Policy = "exact" }],
        };

        var resolver = new EffectivePolicyResolver(config);
        var pin = Pin(DependencyScope.Packages) with { GroupCaption = "SDK package injections", MetadataPolicy = "disable" };
        await Assert.That(Resolve(resolver, pin)).IsEqualTo("disable");
    }

    // Nothing validates item metadata when it is read, so the one policy string that can be nonsense is the
    // one a pin states for itself. The message names the file and the pin, which is what a reader needs.
    [Test]
    public async Task Resolve_OfAPinStatingNonsense_Fails()
    {
        var resolver = new EffectivePolicyResolver(new DependenciesConfig());
        var pin = Pin(DependencyScope.Packages) with { MetadataPolicy = "sometimes" };
        var exception = await Assert.That(() => resolver.Resolve(pin)).Throws<BuildFailedException>();
        await Assert.That(exception!.Message).Contains("sometimes");
        await Assert.That(exception.Message).Contains("Serilog");
        await Assert.That(exception.Message).Contains("Directory.Packages.props");
    }

    private static string Resolve(EffectivePolicyResolver resolver, DependencyPin pin) => resolver.Resolve(pin).ToString();

    private static DependencyPin Pin(DependencyScope scope, string id = "Serilog")
        => DependencyPin.Create(scope, id, "4.0.0", "Directory.Packages.props");

    private static DependenciesConfig GroupConfig(string policy)
        => new()
        {
            AdditionalPackages =
            [
                new AdditionalPackagesConfig
                {
                    Caption = "SDK package injections",
                    Files = "src/Sdk/PackageVersions.props",
                    Items = "BV_PackageVersion",
                    Policy = policy,
                },
            ],
        };
}
