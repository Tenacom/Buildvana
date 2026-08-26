// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Tool.Subcommands;
using NuGet.Versioning;

internal sealed class SelfUpdateSettingsTests
{
    [Test]
    public async Task Parse_WithoutOptions_LeavesForceOff()
    {
        var settings = SelfUpdateSettings.Parse([]);

        await Assert.That(settings.Force).IsFalse();
    }

    [Test]
    public async Task Parse_WithForce_SetsForce()
    {
        var settings = SelfUpdateSettings.Parse(["--force"]);

        await Assert.That(settings.Force).IsTrue();
    }

    [Test]
    public async Task Parse_WithoutTo_LeavesToNull()
    {
        var settings = SelfUpdateSettings.Parse([]);

        await Assert.That(settings.To).IsNull();
        await Assert.That(settings.ResolveTo()).IsNull();
    }

    [Test]
    [Arguments("--to", "2.1.40-preview")]
    [Arguments("--to=2.1.40-preview")]
    public async Task Parse_WithTo_SetsTo(params string[] options)
    {
        var settings = SelfUpdateSettings.Parse(options);

        await Assert.That(settings.To).IsEqualTo("2.1.40-preview");
        await Assert.That(settings.ResolveTo()).IsEqualTo(NuGetVersion.Parse("2.1.40-preview"));
    }

    [Test]
    public async Task ResolveTo_WithInvalidVersion_Fails()
    {
        var settings = SelfUpdateSettings.Parse(["--to", "not-a-version"]);

        var exception = await Assert.That(() => _ = settings.ResolveTo()).Throws<BuildFailedException>();

        await Assert.That(exception!.Message).Contains("--to");
        await Assert.That(exception.Message).Contains("not-a-version");
    }
}
