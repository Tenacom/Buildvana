// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Subcommands;

internal sealed class UpdateSettingsTests
{
    [Test]
    public async Task Parse_WithoutOptions_LeavesForceOff()
    {
        var settings = UpdateSettings.Parse([]);

        await Assert.That(settings.Force).IsFalse();
    }

    [Test]
    public async Task Parse_WithForce_SetsForce()
    {
        var settings = UpdateSettings.Parse(["--force"]);

        await Assert.That(settings.Force).IsTrue();
    }
}
