// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Runtime;

internal sealed class PostReleaseHookContextTests
{
    [Test]
    public async Task Load_NoContextFile_Throws()
    {
        var dir = NewDir();
        try
        {
            await Assert.That(() => PostReleaseHookContext.Load(dir)).Throws<BuildvanaRuntimeException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // Serializes the way bv does (same serializer context, resolved by runtime type) and loads the result
    // back, proving the two sides of the hook contract agree.
    [Test]
    public async Task Load_RoundTripsWhatBvWrites()
    {
        var dir = NewDir();
        try
        {
            var written = SampleContext(dir);
            var relativePath = WellKnownPaths.GetHookContextFile(PostReleaseHookContext.Command, PostReleaseHookContext.Moment);
            var path = Path.Combine(dir, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            object boxed = written;
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(boxed, boxed.GetType(), BuildvanaJsonContext.Default)).ConfigureAwait(false);

            var loaded = PostReleaseHookContext.Load(dir);

            await Assert.That(loaded.Paths).IsEqualTo(written.Paths);
            await Assert.That(loaded.Release).IsEqualTo(written.Release);
            await Assert.That(loaded.ProducedPackages["Buildvana.Sdk"]).IsEqualTo("1.2.3-preview");
            await Assert.That(loaded.Dogfooded).IsEqualTo(written.Dogfooded);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_InvalidJson_Throws()
    {
        var dir = NewDir();
        try
        {
            var path = Path.Combine(dir, WellKnownPaths.GetHookContextFile("release", "post-release"));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "{ not json ").ConfigureAwait(false);
            await Assert.That(() => PostReleaseHookContext.Load(dir)).Throws<BuildvanaRuntimeException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static PostReleaseHookContext SampleContext(string home) => new()
    {
        Paths = new()
        {
            HomeDirectory = home,
            ArtifactsDirectory = Path.Combine(home, "artifacts", "Release"),
            ScratchDirectory = Path.Combine(home, WellKnownPaths.ScratchDirectory),
        },
        Release = new()
        {
            Version = "1.2.3",
            SemVer = "1.2.3-preview",
            PreviousVersion = null,
            IsPrerelease = true,
            IsPublicRelease = true,
        },
        ProducedPackages = new Dictionary<string, string> { ["Buildvana.Sdk"] = "1.2.3-preview" },
        Dogfooded = true,
    };

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }
}
