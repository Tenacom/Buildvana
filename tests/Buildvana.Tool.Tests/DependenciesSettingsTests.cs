// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;
using Buildvana.Tool.Subcommands;

internal sealed class DependenciesSettingsTests
{
    [Test]
    public async Task Parse_WithNoOption_NamesNoScope()
    {
        var settings = DependenciesSettings.Parse([]);
        await Assert.That(settings.Included).IsEmpty();
        await Assert.That(settings.Excluded).IsEmpty();
    }

    [Test]
    public async Task Parse_ReadsTheScopesToManage()
    {
        var settings = DependenciesSettings.Parse(["--packages", "--netsdk"]);
        await Assert.That(settings.Included).IsEquivalentTo([DependencyScope.NetSdk, DependencyScope.Packages]);
        await Assert.That(settings.Excluded).IsEmpty();
    }

    [Test]
    public async Task Parse_ReadsTheScopesToLeaveOut()
    {
        var settings = DependenciesSettings.Parse(["--no-tools", "--no-sdks"]);
        await Assert.That(settings.Excluded).IsEquivalentTo([DependencyScope.Sdks, DependencyScope.Tools]);
        await Assert.That(settings.Included).IsEmpty();
    }

    // The two families differ by a prefix, and one must not be read as the other.
    [Test]
    public async Task Parse_TellsAScopeFromItsNegation()
    {
        var settings = DependenciesSettings.Parse(["--no-netsdk"]);
        await Assert.That(settings.Included).IsEmpty();
        await Assert.That(settings.Excluded).IsEquivalentTo([DependencyScope.NetSdk]);
    }
}
