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
    public async Task Resolve_WithEveryScopeDisabled_TakesNone()
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
