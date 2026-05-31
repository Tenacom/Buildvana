// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Buildvana.Core;
using Buildvana.Core.Configuration;

internal sealed class BuildvanaConfigLoaderTests
{
    [Test]
    public async Task Load_NoFile_ReturnsEmptyConfig()
    {
        var dir = NewDir();
        try
        {
            var config = BuildvanaConfigLoader.Load(dir);
            await Assert.That(config.Release).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_ValidConfig_Loads()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.jsonc", """{ "release": { "branches": ["main"] } }""");
            var config = BuildvanaConfigLoader.Load(dir);
            await Assert.That(config.Release!.Branches!.Count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_BothFilesPresent_ThrowsWithoutDiagnostics()
    {
        var dir = NewDir();
        try
        {
            Write(dir, "buildvana.json", "{}");
            Write(dir, "buildvana.jsonc", "{}");
            var exception = Catch(dir);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception!.Diagnostics.Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task Load_InvalidJson_ReportsBV1100()
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
    public async Task Load_SchemaViolation_ReportsCodeAndPosition()
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
    public async Task Load_UnknownProperty_ReportsBV1103()
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

    [Test]
    public async Task Load_BomPrefixedFile_DoesNotOffsetPositions()
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

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Write(string dir, string fileName, string content, bool bom = false)
        => File.WriteAllText(Path.Combine(dir, fileName), content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom));

    private static BuildFailedException? Catch(string dir)
    {
        try
        {
            _ = BuildvanaConfigLoader.Load(dir);
            return null;
        }
        catch (BuildFailedException exception)
        {
            return exception;
        }
    }
}
