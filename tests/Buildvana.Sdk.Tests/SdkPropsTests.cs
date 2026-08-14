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

    // Hooks are projects living under .buildvana/, so the SDK evaluates from there on every hook build.
    // Nothing inside that directory is a marker, which is what keeps a hook's home directory the repository's own.
    [Test]
    public async Task Evaluate_ProjectUnderBuildvanaDirectory_HomeIsRepositoryRoot()
    {
        using var fixture = new SdkPropsFixture();
        fixture.WriteFile("buildvana.jsonc");
        var result = fixture.Evaluate(".buildvana/hooks/release");
        await Assert.That(result.HomeDirectory).IsEqualTo(fixture.RepoDirectory + Path.DirectorySeparatorChar);
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
}
