// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Testing;
using Buildvana.Runtime;
using Buildvana.Tool.Services.Dependencies;

internal sealed class DependencyScopeSelectionTests
{
    private static readonly DependencyScope[] AllScopes =
        [DependencyScope.NetSdk, DependencyScope.Sdks, DependencyScope.Tools, DependencyScope.Packages];

    // An argument names package ids, and the .NET SDK has none.
    [Test]
    public async Task Narrow_WhenAnArgumentNamesPins_LeavesTheNetSdkAlone()
    {
        var scopes = DependencyScopeSelection.Narrow(AllScopes.ToHashSet(), namesPins: true, statesVersion: false);
        await Assert.That(scopes).IsEquivalentTo([DependencyScope.Sdks, DependencyScope.Tools, DependencyScope.Packages]);
    }

    [Test]
    public async Task Narrow_WithNeitherAnArgumentNorAVersion_ChangesNothing()
    {
        var scopes = DependencyScopeSelection.Narrow(AllScopes.ToHashSet(), namesPins: false, statesVersion: false);
        await Assert.That(scopes).IsEquivalentTo(AllScopes);
    }

    [Test]
    public async Task Narrow_WithAVersionForTheNetSdkAlone_ChangesNothing()
    {
        var selected = new HashSet<DependencyScope> { DependencyScope.NetSdk };
        var scopes = DependencyScopeSelection.Narrow(selected, namesPins: false, statesVersion: true);
        await Assert.That(scopes).IsEquivalentTo([DependencyScope.NetSdk]);
    }

    // Without an argument the stated version is the baseline's, so it must not reach another scope's pins.
    [Test]
    public async Task Narrow_WithAVersionAndAnotherScopeSelected_IsRefused()
    {
        var exception = await Assert
            .That(() => DependencyScopeSelection.Narrow(AllScopes.ToHashSet(), namesPins: false, statesVersion: true))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
    }

    [Test]
    public async Task Narrow_WithAVersionAndTheNetSdkNotSelected_IsRefused()
    {
        var selected = new HashSet<DependencyScope> { DependencyScope.Packages };
        await Assert.That(() => DependencyScopeSelection.Narrow(selected, namesPins: false, statesVersion: true))
            .Throws<BuildFailedException>();
    }

    [Test]
    public async Task Resolve_WithNoFlag_TakesEveryManagedScope()
    {
        await Assert.That(Resolve([], [])).IsEquivalentTo(AllScopes);
    }

    [Test]
    public async Task Resolve_WithScopesNamed_TakesThoseAlone()
    {
        var selected = Resolve([DependencyScope.NetSdk, DependencyScope.Tools], []);
        await Assert.That(selected).IsEquivalentTo([DependencyScope.NetSdk, DependencyScope.Tools]);
    }

    [Test]
    public async Task Resolve_WithScopesExcluded_TakesTheRest()
    {
        var selected = Resolve([], [DependencyScope.Packages]);
        await Assert.That(selected).IsEquivalentTo([DependencyScope.NetSdk, DependencyScope.Sdks, DependencyScope.Tools]);
    }

    // Naming a scope to manage and another to leave out states the selection twice, and the two statements
    // can disagree.
    [Test]
    public async Task Resolve_MixingTheTwoFamilies_IsAUsageError()
    {
        var exception = await Assert.That(() => Resolve([DependencyScope.NetSdk], [DependencyScope.Packages]))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(2);
    }

    [Test]
    public async Task Resolve_LeavesOutAScopeConfigurationDisables()
    {
        var selected = Resolve([], [], Disabling(tools: "disable"));
        await Assert.That(selected).IsEquivalentTo([DependencyScope.NetSdk, DependencyScope.Sdks, DependencyScope.Packages]);
    }

    // A flag restricts a selection; it does not create one. Asking for a scope the configuration file
    // disables would otherwise read as a scope with nothing in it.
    [Test]
    public async Task Resolve_NamingADisabledScope_WarnsAndLeavesItOut()
    {
        var reporter = new CaptureReporter();
        var selected = DependencyScopeSelection.Resolve([DependencyScope.Tools], [], Disabling(tools: "disable"), reporter);
        await Assert.That(selected).IsEmpty();
        await Assert.That(reporter.Messages.Single().Level).IsEqualTo(MessageLevel.Warning);
        await Assert.That(reporter.Messages.Single().Message).Contains("tools");
    }

    // Asking for a disabled scope not to be managed states what is already the case.
    [Test]
    public async Task Resolve_ExcludingADisabledScope_SaysNothing()
    {
        var reporter = new CaptureReporter();
        var selected = DependencyScopeSelection.Resolve([], [DependencyScope.Tools], Disabling(tools: "disable"), reporter);
        await Assert.That(selected).IsEquivalentTo([DependencyScope.NetSdk, DependencyScope.Sdks, DependencyScope.Packages]);
        await Assert.That(reporter.Messages).IsEmpty();
    }

    [Test]
    public async Task Resolve_WithAllScopesDisabled_TakesNone()
    {
        var config = new DependenciesConfig
        {
            Scopes = new DependencyScopesConfig
            {
                NetSdk = "disable",
                Sdks = "disable",
                Tools = "disable",
                Packages = "disable",
            },
        };

        await Assert.That(Resolve([], [], config)).IsEmpty();
    }

    private static IReadOnlySet<DependencyScope> Resolve(
        IReadOnlyList<DependencyScope> included,
        IReadOnlyList<DependencyScope> excluded,
        DependenciesConfig? config = null)
        => DependencyScopeSelection.Resolve(included, excluded, config ?? new DependenciesConfig(), NullReporter.Instance);

    private static DependenciesConfig Disabling(string tools)
        => new() { Scopes = new DependencyScopesConfig { Tools = tools } };
}
