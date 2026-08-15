// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core;
using Buildvana.Core.Configuration;
using Buildvana.Core.HomeDirectory;
using Buildvana.Runtime;

internal sealed class BuildvanaJsonConfigProviderTests
{
    [Test]
    public async Task Config_NoFile_IsEmptyConfig()
    {
        var dir = NewDir();
        try
        {
            var config = NewProvider(dir).Config;
            await Assert.That(config.Release).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_ValidConfig_Loads()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", """{ "release": { "branches": ["main"] } }""");
            var config = NewProvider(dir).Config;
            await Assert.That(config.Release!.Branches!.Count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // An empty JSON object is a valid configuration; it makes a configuration file usable
    // as a pure home-directory marker (the replacement for the retired .buildvana-home file).
    [Test]
    public async Task Config_EmptyObject_Loads()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", "{}");
            var config = NewProvider(dir).Config;
            await Assert.That(config.Release).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Path_NoFile_IsNull()
    {
        var dir = NewDir();
        try
        {
            await Assert.That(NewProvider(dir).Path).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    [Arguments("buildvana.json")]
    [Arguments("buildvana.jsonc")]
    public async Task Path_OneFile_IsItsPath(string fileName)
    {
        var dir = NewDir();
        try
        {
            Write(dir, fileName, "{}");
            await Assert.That(NewProvider(dir).Path).IsEqualTo(Path.Combine(dir, fileName));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // Deleting the file between the two reads is what proves the answer is cached rather than recomputed:
    // one run gets one answer, whichever of the provider's two facts is asked for first.
    [Test]
    public async Task PathAndConfig_ReadAfterFileRemoval_KeepTheFirstAnswer()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", """{ "release": { "branches": ["main"] } }""");
            var provider = NewProvider(dir);
            var path = provider.Path;
            var config = provider.Config;

            File.Delete(Path.Combine(dir, "buildvana.jsonc"));

            await Assert.That(provider.Path).IsEqualTo(path);
            await Assert.That(provider.Config).IsSameReferenceAs(config);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // Reading the configuration without having read the path first must find the file just the same:
    // the two are resolved independently on demand, from a single probe.
    [Test]
    public async Task Config_ReadBeforePath_FindsTheFile()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", """{ "release": { "branches": ["main"] } }""");
            var provider = NewProvider(dir);

            await Assert.That(provider.Config.Release!.Branches!.Count).IsEqualTo(1);
            await Assert.That(provider.Path).IsEqualTo(Path.Combine(dir, "buildvana.jsonc"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Path_BothFilesPresent_Throws()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", "{}");
            Write(dir, "buildvana.jsonc", "{}");
            var provider = NewProvider(dir);

            _ = await Assert.That(() => provider.Path).Throws<BuildFailedException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_BothFilesPresent_ThrowsNamingBothWithoutDiagnostics()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", "{}");
            Write(dir, "buildvana.jsonc", "{}");
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Message).Contains(Path.Combine(dir, "buildvana.json"));
            await Assert.That(exception.Message).Contains(Path.Combine(dir, "buildvana.jsonc"));
            await Assert.That(exception.Diagnostics.Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_InvalidJson_ReportsBV1100()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", "{ not json ");
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Diagnostics.Count).IsEqualTo(1);
            await Assert.That(exception.Diagnostics[0].Code).IsEqualTo("BV1100");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_SchemaViolation_ReportsCodeAndPosition()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", "{\n  \"release\": { \"branches\": [\"main\", null] }\n}");
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Diagnostics.Count).IsEqualTo(1);
            await Assert.That(exception.Diagnostics[0].Code).IsEqualTo("BV1101");
            await Assert.That(exception.Diagnostics[0].Line).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_UnknownProperty_ReportsBV1103()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", """{ "bogus": 1 }""");
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Diagnostics[0].Code).IsEqualTo("BV1103");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // The schema requires source and apiKeyEnv whenever a feed is stated, so a half-written feed fails at
    // configuration load rather than at push time.
    [Test]
    public async Task Config_FeedWithoutApiKeyEnv_ReportsBV1104()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", """{ "nuget": { "feeds": { "release": { "source": "https://nuget.example" } } } }""");
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Diagnostics[0].Code).IsEqualTo("BV1104");
            await Assert.That(exception.Diagnostics[0].Message).Contains("apiKeyEnv");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_IdentityWithoutEmail_ReportsBV1104()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", """{ "git": { "identity": { "name": "Buildvana Bot" } } }""");
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Diagnostics[0].Code).IsEqualTo("BV1104");
            await Assert.That(exception.Diagnostics[0].Message).Contains("email");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // A configuration file under a subdirectory — the .buildvana directory included — is not the
    // configuration file: the file lives in the home directory itself.
    [Test]
    public async Task Path_FileInBuildvanaDirectory_IsNotFound()
    {
        var dir = NewDir();
        try
        {
            Write(dir, Path.Combine(".buildvana", "buildvana.json"), "{}");
            await Assert.That(NewProvider(dir).Path).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task LoadFile_NullPath_IsEmptyConfig()
    {
        await Assert.That(BuildvanaJsonConfigProvider.LoadFile(null).Release).IsNull();
    }

    // Loading by explicit path ignores sibling candidates: the exactly-one rule belongs to the search,
    // not to the loader.
    [Test]
    public async Task LoadFile_KnownPath_LoadsItRegardlessOfSiblings()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", """{ "release": { "branches": ["main"] } }""");
            Write(dir, "buildvana.jsonc", "{}");
            var config = BuildvanaJsonConfigProvider.LoadFile(Path.Combine(dir, "buildvana.json"));
            await Assert.That(config.Release!.Branches!.Count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_TypedContent_LoadsEnumsAndNestedSections()
    {
        var dir = NewDir();
        try
        {
            const string content = """
                {
                  // A comment and a trailing comma prove that jsonc niceties are accepted.
                  "versioning": { "assemblyVersionPrecision": "minor" },
                  "nuget": { "feeds": { "release": { "source": "https://nuget.example/v3/index.json", "apiKeyEnv": "KEY" } } },
                }
                """;
            Write(dir, "buildvana.jsonc", content);
            var config = NewProvider(dir).Config;
            await Assert.That(config.Versioning!.AssemblyVersionPrecision).IsEqualTo(AssemblyVersionPrecision.Minor);
            await Assert.That(config.NuGet!.Feeds!.Release!.ApiKeyEnv).IsEqualTo("KEY");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // An exclusively-locked file is the portable trigger for the read-failure branch: File.Exists stays true
    // (it checks attributes, not access), while reading fails on every platform.
    [Test]
    public async Task Config_UnreadableFile_Throws()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", "{}");
            var path = Path.Combine(dir, "buildvana.jsonc");
            await using var locker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None).ConfigureAwait(false);
            _ = await Assert.That(() => NewProvider(dir).Config).Throws<BuildFailedException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Config_BomPrefixedFile_DoesNotOffsetPositions()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", "{\n  \"release\": { \"branches\": [42] }\n}", bom: true);
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Diagnostics[0].Line).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static BuildvanaJsonConfigProvider NewProvider(string dir) => new(new FixedHomeDirectoryProvider(dir));

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Write(string dir, string fileName, string content, bool bom = false)
    {
        var path = Path.Combine(dir, fileName);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom));
    }

    private static BuildFailedException? Catch(string dir)
    {
        try
        {
            _ = NewProvider(dir).Config;
            return null;
        }
        catch (BuildFailedException exception)
        {
            return exception;
        }
    }
}
