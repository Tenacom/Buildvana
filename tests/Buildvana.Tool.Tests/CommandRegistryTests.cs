// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure.Execution;

internal sealed class CommandRegistryTests
{
    [Test]
    public async Task Find_IsCaseInsensitive()
    {
        await Assert.That(CommandRegistry.Find("BUILD")?.Name).IsEqualTo("build");
    }

    [Test]
    public async Task Find_ReturnsNull_ForUnknownCommand()
    {
        await Assert.That(CommandRegistry.Find("frobnicate")).IsNull();
    }

    [Test]
    public async Task Release_CarriesItsSettingsType()
    {
        await Assert.That(CommandRegistry.Find("release")?.SettingsType).IsNotNull();
    }

    [Test]
    public async Task PipelineCommands_AppearInExecutionOrderBeforeRelease()
    {
        var names = string.Join(",", CommandRegistry.Commands.Select(c => c.Name));
        await Assert.That(names).IsEqualTo("clean,restore,build,test,pack,release");
    }
}
