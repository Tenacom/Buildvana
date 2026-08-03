// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Buildvana.Tool.Utilities;

internal sealed class PackageArtifactsHelperTests
{
    [Test]
    public async Task DiscoverProducedPackages_WithoutArtifactsDirectory_ReturnsEmpty()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var result = PackageArtifactsHelper.DiscoverProducedPackages(missingPath, "1.0.0");

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DiscoverProducedPackages_MapsMatchingPackagesAndIgnoresOthers()
    {
        using var home = new TempHome();
        home.WriteFile("Buildvana.Sdk.2.0.0.nupkg", string.Empty);
        home.WriteFile("Buildvana.Tool.2.0.0.nupkg", string.Empty);
        home.WriteFile("Other.1.9.0.nupkg", string.Empty);
        home.WriteFile("readme.txt", string.Empty);

        var result = PackageArtifactsHelper.DiscoverProducedPackages(home.RootPath, "2.0.0");

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result["Buildvana.Sdk"]).IsEqualTo("2.0.0");
        await Assert.That(result["Buildvana.Tool"]).IsEqualTo("2.0.0");
    }

    [Test]
    public async Task DiscoverProducedPackages_MatchesVersionSuffixCaseInsensitively()
    {
        using var home = new TempHome();
        home.WriteFile("Buildvana.Sdk.2.0.0-PREVIEW.nupkg", string.Empty);

        var result = PackageArtifactsHelper.DiscoverProducedPackages(home.RootPath, "2.0.0-preview");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result["Buildvana.Sdk"]).IsEqualTo("2.0.0-preview");
    }
}
