// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.HomeDirectory;

internal sealed class HomeDirectoryDiscoveryTests
{
    [Test]
    [Arguments("buildvana.json")]
    [Arguments("buildvana.jsonc")]
    [Arguments(".buildvana/buildvana.json")]
    [Arguments(".buildvana/buildvana.jsonc")]
    [Arguments(".git")]
    [Arguments(".git/HEAD")]
    public async Task TryDiscover_MarkerInStartDirectory_MarksIt(string marker)
    {
        var root = NewDir();
        try
        {
            WriteFile(root, marker);
            var found = HomeDirectoryDiscovery.TryDiscover(root, out var home);
            await Assert.That(found).IsTrue();
            await Assert.That(home).IsEqualTo(WithTrailingSeparator(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task TryDiscover_ConfigInSubdirectory_MarksContainingDirectory()
    {
        var root = NewDir();
        try
        {
            WriteFile(root, ".buildvana/buildvana.jsonc");
            var start = Path.Combine(root, "src", "MyProject");
            _ = Directory.CreateDirectory(start);
            var found = HomeDirectoryDiscovery.TryDiscover(start, out var home);
            await Assert.That(found).IsTrue();
            await Assert.That(home).IsEqualTo(WithTrailingSeparator(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task TryDiscover_BareSubdirectory_IsNotMarker()
    {
        var root = NewDir();
        try
        {
            WriteFile(root, ".git/HEAD");
            var child = Path.Combine(root, "child");
            _ = Directory.CreateDirectory(Path.Combine(child, ".buildvana"));
            var found = HomeDirectoryDiscovery.TryDiscover(child, out var home);
            await Assert.That(found).IsTrue();
            await Assert.That(home).IsEqualTo(WithTrailingSeparator(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task TryDiscover_MultipleMarkers_NearestWins()
    {
        var root = NewDir();
        try
        {
            WriteFile(root, ".git/HEAD");
            var nested = Path.Combine(root, "nested");
            WriteFile(nested, ".buildvana/buildvana.json");
            var start = Path.Combine(nested, "src");
            _ = Directory.CreateDirectory(start);
            var found = HomeDirectoryDiscovery.TryDiscover(start, out var home);
            await Assert.That(found).IsTrue();
            await Assert.That(home).IsEqualTo(WithTrailingSeparator(nested));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteFile(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    private static string WithTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
