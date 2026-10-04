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

    [Test]
    public async Task Parse_WithoutOptions_LeavesPreviewAndRepairOff()
    {
        var settings = SelfUpdateSettings.Parse([]);

        await Assert.That(settings.Preview).IsFalse();
        await Assert.That(settings.Repair).IsFalse();
    }

    [Test]
    public async Task Parse_WithPreview_SetsPreview()
    {
        var settings = SelfUpdateSettings.Parse(["--preview"]);

        await Assert.That(settings.Preview).IsTrue();
    }

    [Test]
    public async Task Parse_WithRepair_SetsRepair()
    {
        var settings = SelfUpdateSettings.Parse(["--repair"]);

        await Assert.That(settings.Repair).IsTrue();
    }

    // --force says whether a downgrade is allowed, not where the repository goes, so it goes with every
    // option that names the target.
    [Test]
    [Arguments("--preview")]
    [Arguments("--repair")]
    [Arguments("--to=2.1.40-preview")]
    public async Task Parse_WithForce_AndATargetOption_Succeeds(string option)
    {
        var settings = SelfUpdateSettings.Parse(["--force", option]);

        await Assert.That(settings.Force).IsTrue();
    }

    [Test]
    [Arguments("--preview", "--repair")]
    [Arguments("--preview", "--to=2.1.40-preview")]
    [Arguments("--repair", "--to=2.1.40-preview")]
    [Arguments("--preview", "--repair", "--to=2.1.40-preview")]
    public async Task Parse_WithTwoTargetOptions_Fails(params string[] options)
    {
        var exception = await Assert.That(() => _ = SelfUpdateSettings.Parse(options)).Throws<BuildFailedException>();

        await Assert.That(exception!.ExitCode).IsEqualTo(ExitCodes.Usage);
        foreach (var option in options)
        {
            await Assert.That(exception.Message).Contains(option.Split('=')[0]);
        }
    }
}
