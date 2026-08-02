// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

internal sealed class SdkPropsTests
{
    // Every test fixture plants at least one marker inside the temporary repository, so discovery
    // always resolves within the fixture (the nearest marker wins) and stray markers above the
    // temporary directory cannot interfere. The no-marker case (BVSDK1003) is deliberately not
    // tested: GetDirectoryNameOfFileAbove walks to the drive root, which cannot be made hermetic.
    [Test]
    [Arguments("buildvana.json")]
    [Arguments("buildvana.jsonc")]
    [Arguments(".buildvana/buildvana.json")]
    [Arguments(".buildvana/buildvana.jsonc")]
    [Arguments(".git")]
    [Arguments(".git/HEAD")]
    public async Task Evaluate_MarkerInRepoRoot_SetsHomeDirectory(string marker)
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(marker);
        var result = fixture.Evaluate();
        await Assert.That(result.HomeDirectory).IsEqualTo(fixture.RepoDirectory + Path.DirectorySeparatorChar);
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task Evaluate_ConfigInSubdirectory_HomeIsContainingDirectory()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".git/HEAD");
        fixture.WriteFile("nested/.buildvana/buildvana.jsonc");
        var result = fixture.Evaluate("nested/src/Test");
        var expected = Path.Combine(fixture.RepoDirectory, "nested") + Path.DirectorySeparatorChar;
        await Assert.That(result.HomeDirectory).IsEqualTo(expected);
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task Evaluate_BothVariantsInHomeDirectory_ReportsBVSDK1005()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile("buildvana.json");
        fixture.WriteFile("buildvana.jsonc");
        var result = fixture.Evaluate();
        var error = result.Errors.Single(static e => e.Code == "BVSDK1005");
        await Assert.That(error.Text).Contains("buildvana.json");
        await Assert.That(error.Text).Contains("buildvana.jsonc");
    }

    [Test]
    public async Task Evaluate_ConfigInRootAndSubdirectory_ReportsBVSDK1005NamingAllOffenders()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile("buildvana.jsonc");
        fixture.WriteFile(".buildvana/buildvana.json");
        var result = fixture.Evaluate();
        var error = result.Errors.Single(static e => e.Code == "BVSDK1005");
        await Assert.That(error.Text).Contains("buildvana.jsonc");
        await Assert.That(error.Text).Contains(".buildvana");
    }

    [Test]
    public async Task Evaluate_SingleConfigFile_ReportsNoError()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile(".buildvana/buildvana.jsonc");
        var result = fixture.Evaluate();
        await Assert.That(result.Errors).IsEmpty();
    }
}
