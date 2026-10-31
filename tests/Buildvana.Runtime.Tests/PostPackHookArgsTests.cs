// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Runtime;

internal sealed class PostPackHookArgsTests
{
    // Serializes the way bv does (same serializer context, resolved by runtime type) and loads the result
    // back, proving the two sides of the hook contract agree. PostReleaseHookArgsTests covers the loading
    // failures, which HookArgs shares between every args type.
    [Test]
    public async Task Load_RoundTripsWhatBvWrites()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        try
        {
            var written = SampleArgs(dir);
            var relativePath = WellKnownPaths.GetHookArgsFile(PostPackHookArgs.Context, PostPackHookArgs.Event);
            var path = Path.Combine(dir, relativePath);
            _ = Directory.CreateDirectory(await Assert.That(Path.GetDirectoryName(path)).IsNotNull());
            object boxed = written;
            var json = JsonSerializer.Serialize(boxed, boxed.GetType(), BuildvanaJsonContext.Default);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);

            var loaded = PostPackHookArgs.Load(dir);

            // Collection-typed members compare by reference, so record equality cannot vouch for the round
            // trip. Re-serializing what was loaded and comparing documents does, for every member at once.
            object reboxed = loaded;
            var rewritten = JsonSerializer.Serialize(reboxed, reboxed.GetType(), BuildvanaJsonContext.Default);
            await Assert.That(rewritten).IsEqualTo(json);
            await Assert.That(relativePath).IsEqualTo(".buildvana-temp/hook-args/release/post-pack.json");
            await Assert.That(loaded.Release).IsEqualTo(written.Release);
            await Assert.That(loaded.ProducedPackages["A"]).IsEqualTo("1.2.3-preview");
            await Assert.That(loaded.Solution.Projects[0].PackageId).IsEqualTo("A");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static PostPackHookArgs SampleArgs(string home) => new() {
        RuntimeInfo = new() {
            Version = "1.2.3-preview",
            DelegatingVersion = null,
            HomeDirectory = home,
            ArtifactsDirectory = Path.Combine(home, "artifacts", "Release"),
            ScratchDirectory = Path.Combine(home, WellKnownPaths.ScratchDirectory),
            ConfigFile = null,
            Configuration = new(),
        },
        Release = new() {
            Version = "1.2.3",
            SemVer = "1.2.3-preview",
            PreviousVersion = "1.2.2-preview",
            IsPrerelease = true,
            IsPublicRelease = true,
        },
        ProducedPackages = new Dictionary<string, string> { ["A"] = "1.2.3-preview" },
        Solution = new() {
            SolutionFile = Path.Combine(home, "Test.slnx"),
            Configuration = "Release",
            Projects = [
                new() {
                    ProjectFile = Path.Combine(home, "src", "A", "A.csproj"),
                    IsPackable = true,
                    PackAsTool = false,
                    PackageId = "A",
                    TargetFrameworks = [
                        new() {
                            TargetFramework = "net10.0",
                            TargetPath = Path.Combine(home, "src", "A", "bin", "Release", "net10.0", "A.dll"),
                            DocumentationFile = Path.Combine(home, "src", "A", "bin", "Release", "net10.0", "A.xml"),
                            OutputType = "Library",
                            IsTestProject = false,
                        },
                    ],
                },
            ],
        },
    };
}
