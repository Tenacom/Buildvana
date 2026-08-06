// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Runtime;

internal sealed class BuildvanaConfigLoadTests
{
    [Test]
    public async Task Load_NoFile_ReturnsEmptyConfig()
    {
        var dir = NewDir();
        try
        {
            var config = BuildvanaConfig.Load(dir);
            await Assert.That(config.Release).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_ValidJsoncFile_LoadsTypedConfig()
    {
        var dir = NewDir();
        try
        {
            const string json = """
                {
                    // A comment, and a trailing comma below.
                    "release": { "branches": ["main"] },
                    "versioning": { "assemblyVersionPrecision": "minor" },
                    "nuget": { "feeds": { "release": { "source": "https://release.example", "apiKeyEnv": "KEY" } } },
                }
                """;
            Write(dir, "buildvana.jsonc", json);
            var config = BuildvanaConfig.Load(dir);
            await Assert.That(config.Release!.Branches!.Count).IsEqualTo(1);
            await Assert.That(config.Versioning!.AssemblyVersionPrecision).IsEqualTo(AssemblyVersionPrecision.Minor);
            await Assert.That(config.NuGet!.Feeds!.Release!.Source).IsEqualTo("https://release.example");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_ConfigInSubdirectory_Loads()
    {
        var dir = NewDir();
        try
        {
            Write(dir, Path.Combine(".buildvana", "buildvana.json"), """{ "release": { "checkPublicApi": true } }""");
            var config = BuildvanaConfig.Load(dir);
            await Assert.That(config.Release!.CheckPublicApi).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_MultipleFiles_Throws()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", "{}");
            Write(dir, "buildvana.jsonc", "{}");
            await Assert.That(() => BuildvanaConfig.Load(dir)).Throws<BuildvanaRuntimeException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // Reading is strict: bv validates the file before any hook can read it, so an unknown member here
    // means the file changed after validation, and failing beats guessing.
    [Test]
    public async Task Load_UnknownProperty_Throws()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", """{ "bogus": 1 }""");
            await Assert.That(() => BuildvanaConfig.Load(dir)).Throws<BuildvanaRuntimeException>();
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
    public async Task Load_UnreadableFile_Throws()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", "{}");
            var path = Path.Combine(dir, "buildvana.json");
            using var locker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

            var act = () => BuildvanaConfig.Load(dir);

            var exception = await Assert.That(act).Throws<BuildvanaRuntimeException>();
            await Assert.That(exception!.Message).Contains("Could not read from");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task FindFile_NoFile_ReturnsNull()
    {
        var dir = NewDir();
        try
        {
            await Assert.That(BuildvanaConfig.FindFile(dir)).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task FindFile_OneFile_ReturnsItsPath()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", "{}");
            await Assert.That(BuildvanaConfig.FindFile(dir)).IsEqualTo(Path.Combine(dir, "buildvana.jsonc"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Write(string dir, string fileName, string content)
    {
        var path = Path.Combine(dir, fileName);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
