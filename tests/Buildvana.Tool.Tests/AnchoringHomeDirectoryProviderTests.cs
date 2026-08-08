// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Testing;
using Buildvana.Tool.Infrastructure;

internal sealed class AnchoringHomeDirectoryProviderTests
{
    [Test]
    [NotInParallel]
    public async Task HomeDirectory_OnFirstRead_MakesHomeTheCurrentDirectory()
    {
        using var home = new TempHome();
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            var provider = new AnchoringHomeDirectoryProvider(home.Provider);
            await Assert.That(Directory.GetCurrentDirectory()).IsEqualTo(previousCurrentDirectory);

            var homeDirectory = provider.HomeDirectory;

            await Assert.That(Path.TrimEndingDirectorySeparator(homeDirectory)).IsEqualTo(home.RootPath);
            await Assert.That(Directory.GetCurrentDirectory()).IsEqualTo(home.RootPath);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
        }
    }

    [Test]
    [NotInParallel]
    public async Task HomeDirectory_WhenDiscoveryFails_PropagatesAndLeavesCurrentDirectoryAlone()
    {
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        var provider = new AnchoringHomeDirectoryProvider(new ThrowingHomeDirectoryProvider());

        _ = await Assert.That(() => provider.HomeDirectory).Throws<BuildFailedException>();
        await Assert.That(Directory.GetCurrentDirectory()).IsEqualTo(previousCurrentDirectory);
    }
}
