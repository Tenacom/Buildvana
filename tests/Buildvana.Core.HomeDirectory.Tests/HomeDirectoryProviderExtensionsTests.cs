// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;

internal sealed class HomeDirectoryProviderExtensionsTests
{
    [Test]
    public async Task GetFullPath_RelativePath_ResolvesAgainstHomeDirectory()
    {
        var home = Directory.CreateTempSubdirectory("bv-test-home-");
        try
        {
            var provider = new FixedHomeDirectoryProvider(home.FullName);

            var path = provider.GetFullPath(Path.Combine("artifacts", "Release"));

            await Assert.That(path).IsEqualTo(Path.Combine(home.FullName, "artifacts", "Release"));
        }
        finally
        {
            home.Delete();
        }
    }

    [Test]
    public async Task GetFullPath_AbsolutePath_ReturnsItNormalized()
    {
        var home = Directory.CreateTempSubdirectory("bv-test-home-");
        try
        {
            var provider = new FixedHomeDirectoryProvider(home.FullName);
            var absolutePath = Path.Combine(home.FullName, "sub", "..", "artifacts");

            var path = provider.GetFullPath(absolutePath);

            await Assert.That(path).IsEqualTo(Path.Combine(home.FullName, "artifacts"));
        }
        finally
        {
            home.Delete();
        }
    }

    // Home directories carry a trailing separator by contract, as the discovering provider's do.
    [Test]
    public async Task GetFullPath_HomeDirectoryWithTrailingSeparator_ResolvesTheSameWay()
    {
        var home = Directory.CreateTempSubdirectory("bv-test-home-");
        try
        {
            var provider = new FixedHomeDirectoryProvider(home.FullName + Path.DirectorySeparatorChar);

            var path = provider.GetFullPath("VERSION");

            await Assert.That(path).IsEqualTo(Path.Combine(home.FullName, "VERSION"));
        }
        finally
        {
            home.Delete();
        }
    }
}
