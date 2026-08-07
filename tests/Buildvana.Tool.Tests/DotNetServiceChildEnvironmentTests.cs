// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Services;

internal sealed class DotNetServiceChildEnvironmentTests
{
    [Test]
    public async Task ChildEnvironment_WithoutVariables_RemovesTheDelegationMarker()
    {
        var environment = DotNetService.ChildEnvironment(null);

        await Assert.That(environment.Count).IsEqualTo(1);
        await Assert.That(environment[DelegationService.DelegatedEnvVar]).IsNull();
    }

    [Test]
    public async Task ChildEnvironment_LayersTheGivenVariablesOnTop()
    {
        var given = new Dictionary<string, string?> { ["KEY"] = "value", ["REMOVE_ME"] = null };

        var environment = DotNetService.ChildEnvironment(given);

        await Assert.That(environment.Count).IsEqualTo(3);
        await Assert.That(environment["KEY"]).IsEqualTo("value");
        await Assert.That(environment["REMOVE_ME"]).IsNull();
        await Assert.That(environment[DelegationService.DelegatedEnvVar]).IsNull();
    }

    // The marker is not meant to be set by hand, but an explicitly configured value always wins over bv's
    // defaults, like everywhere else in the configuration.
    [Test]
    public async Task ChildEnvironment_LetsAnExplicitlyConfiguredMarkerWin()
    {
        var given = new Dictionary<string, string?> { [DelegationService.DelegatedEnvVar] = "2.1.40-preview" };

        var environment = DotNetService.ChildEnvironment(given);

        await Assert.That(environment.Count).IsEqualTo(1);
        await Assert.That(environment[DelegationService.DelegatedEnvVar]).IsEqualTo("2.1.40-preview");
    }
}
