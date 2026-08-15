// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Runtime;

internal sealed class PostReleaseHookArgsTests
{
    [Test]
    public async Task Load_NoArgsFile_Throws()
    {
        var dir = NewDir();
        try
        {
            await Assert.That(() => PostReleaseHookArgs.Load(dir)).Throws<BuildvanaRuntimeException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // Serializes the way bv does (same serializer context, resolved by runtime type) and loads the result
    // back, proving the two sides of the hook contract agree.
    // Both configuration-file cases run because ConfigFile is required and nullable: a repository with no
    // configuration file round-trips only as long as the serializer writes the null, and a default ignore
    // condition on the context would take that away. Here that costs a failing test, not a failing release.
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Load_RoundTripsWhatBvWrites(bool withConfigFile)
    {
        var dir = NewDir();
        try
        {
            var written = SampleArgs(dir, withConfigFile);
            var relativePath = WellKnownPaths.GetHookArgsFile(PostReleaseHookArgs.Context, PostReleaseHookArgs.Event);
            var path = Path.Combine(dir, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            object boxed = written;
            var json = JsonSerializer.Serialize(boxed, boxed.GetType(), BuildvanaJsonContext.Default);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);

            var loaded = PostReleaseHookArgs.Load(dir);

            // RuntimeInfo embeds the resolved configuration, whose collection-typed members compare by
            // reference, so record equality cannot vouch for the round trip. Re-serializing what was loaded
            // and comparing documents does, for every member at once.
            object reboxed = loaded;
            var rewritten = JsonSerializer.Serialize(reboxed, reboxed.GetType(), BuildvanaJsonContext.Default);
            await Assert.That(rewritten).IsEqualTo(json);
            await Assert.That(loaded.RuntimeInfo.ConfigFile).IsEqualTo(written.RuntimeInfo.ConfigFile);
            await Assert.That(loaded.RuntimeInfo.Configuration.Release.Branches[0]).IsEqualTo("^main$");
            await Assert.That(loaded.RuntimeInfo.Configuration.DotNet.Test.Args[0]).IsEqualTo("--coverage");
            await Assert.That(loaded.RuntimeInfo.Configuration.DotNet.Test.Env["RUNNER"]).IsNull();
            await Assert.That(loaded.Release).IsEqualTo(written.Release);
            await Assert.That(loaded.ProducedPackages["Buildvana.Sdk"]).IsEqualTo("1.2.3-preview");
            await Assert.That(loaded.Dogfooding).IsEqualTo(written.Dogfooding);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // The parameterless overload is the shape hooks actually use, and it holds up its end of the contract
    // only because bv runs every hook from the home directory.
    [Test]
    [NotInParallel]
    public async Task Load_WithoutHomeDirectory_ReadsTheArgsFileOfTheCurrentDirectory()
    {
        var dir = NewDir();
        var previousCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            var relativePath = WellKnownPaths.GetHookArgsFile(PostReleaseHookArgs.Context, PostReleaseHookArgs.Event);
            var path = Path.Combine(dir, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            object boxed = SampleArgs(dir);
            var json = JsonSerializer.Serialize(boxed, boxed.GetType(), BuildvanaJsonContext.Default);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
            Directory.SetCurrentDirectory(dir);

            var loaded = PostReleaseHookArgs.Load();

            await Assert.That(loaded.RuntimeInfo.HomeDirectory).IsEqualTo(dir);
            await Assert.That(loaded.Release.SemVer).IsEqualTo("1.2.3-preview");
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCurrentDirectory);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_InvalidJson_Throws()
    {
        var dir = NewDir();
        try
        {
            var path = Path.Combine(dir, WellKnownPaths.GetHookArgsFile("release", "post-release"));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "{ not json ").ConfigureAwait(false);
            await Assert.That(() => PostReleaseHookArgs.Load(dir)).Throws<BuildvanaRuntimeException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // An exclusively-locked file is the portable trigger for the read-failure branch: File.Exists stays true
    // (it checks attributes, not access), while File.ReadAllText fails on every platform — .NET maps FileShare
    // to advisory locks on Unix, so the sharing violation holds there too.
    [Test]
    public async Task Load_UnreadableArgsFile_Throws()
    {
        var dir = NewDir();
        try
        {
            var path = Path.Combine(dir, WellKnownPaths.GetHookArgsFile("release", "post-release"));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "{}").ConfigureAwait(false);
            await using var locker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None).ConfigureAwait(false);

            PostReleaseHookArgs Act() => PostReleaseHookArgs.Load(dir);

            var exception = await Assert.That(Act).Throws<BuildvanaRuntimeException>();
            await Assert.That(exception!.Message).Contains("Could not read from");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static PostReleaseHookArgs SampleArgs(string home, bool withConfigFile = true) => new()
    {
        RuntimeInfo = new()
        {
            Version = "1.2.3-preview",
            DelegatingVersion = "1.2.4",
            HomeDirectory = home,
            ArtifactsDirectory = Path.Combine(home, "artifacts", "Release"),
            ScratchDirectory = Path.Combine(home, WellKnownPaths.ScratchDirectory),
            ConfigFile = withConfigFile ? Path.Combine(home, "buildvana.jsonc") : null,
            Configuration = new()
            {
                Release = new() { Branches = ["^main$"] },
                DotNet = new()
                {
                    Test = new()
                    {
                        Args = ["--coverage"],
                        Env = new Dictionary<string, string?> { ["RUNNER"] = null },
                    },
                },
            },
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
        Dogfooding = true,
    };

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }
}
