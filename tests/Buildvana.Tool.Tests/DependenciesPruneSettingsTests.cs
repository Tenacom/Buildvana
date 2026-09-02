// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Subcommands;

internal sealed class DependenciesPruneSettingsTests
{
    [Test]
    public async Task Parse_WithNoOption_NamesNoScopeAndAppliesWhatItFinds()
    {
        var settings = DependenciesPruneSettings.Parse([]);
        await Assert.That(settings.Included).IsEmpty();
        await Assert.That(settings.Excluded).IsEmpty();
        await Assert.That(settings.Check).IsFalse();
    }

    [Test]
    public async Task Parse_ReadsTheScopesToManage()
    {
        var settings = DependenciesPruneSettings.Parse(["--packages", "--netsdk"]);
        await Assert.That(settings.Included).IsEquivalentTo([DependencyScope.NetSdk, DependencyScope.Packages]);
        await Assert.That(settings.Excluded).IsEmpty();
    }

    [Test]
    public async Task Parse_ReadsTheScopesToLeaveOut()
    {
        var settings = DependenciesPruneSettings.Parse(["--no-tools", "--no-sdks"]);
        await Assert.That(settings.Excluded).IsEquivalentTo([DependencyScope.Sdks, DependencyScope.Tools]);
        await Assert.That(settings.Included).IsEmpty();
    }

    [Test]
    public async Task Parse_ReadsTheCheckFlag()
    {
        var settings = DependenciesPruneSettings.Parse(["--check"]);
        await Assert.That(settings.Check).IsTrue();
    }
}
