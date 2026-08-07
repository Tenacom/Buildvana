// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.Testing;
using Buildvana.Tool.Infrastructure.Delegation;
using NuGet.Versioning;

internal sealed class ToolResolverCacheProbeTests
{
    private static readonly NuGetVersion Pin = NuGetVersion.Parse("2.1.40-preview");

    [Test]
    public async Task IsInstalled_WithMatchingEntryPointingAtExistingExecutable_ReturnsTrue()
    {
        using var home = new TempHome();
        WriteCacheFile(home, CacheText("2.1.40-preview", CreateExecutableFile(home)));
        var probe = new ToolResolverCacheProbe(CacheDirectory(home));

        await Assert.That(probe.IsInstalled("bv", Pin)).IsTrue();
    }

    // NuGet compares release labels case-insensitively, so the probe must too.
    [Test]
    public async Task IsInstalled_MatchesVersionCaseInsensitively()
    {
        using var home = new TempHome();
        WriteCacheFile(home, CacheText("2.1.40-PREVIEW", CreateExecutableFile(home)));
        var probe = new ToolResolverCacheProbe(CacheDirectory(home));

        await Assert.That(probe.IsInstalled("bv", Pin)).IsTrue();
    }

    [Test]
    public async Task IsInstalled_WithoutCacheFile_ReturnsFalse()
    {
        using var home = new TempHome();
        var probe = new ToolResolverCacheProbe(CacheDirectory(home));

        await Assert.That(probe.IsInstalled("bv", Pin)).IsFalse();
    }

    [Test]
    public async Task IsInstalled_WithDifferentVersionOnly_ReturnsFalse()
    {
        using var home = new TempHome();
        WriteCacheFile(home, CacheText("2.1.39-preview", CreateExecutableFile(home)));
        var probe = new ToolResolverCacheProbe(CacheDirectory(home));

        await Assert.That(probe.IsInstalled("bv", Pin)).IsFalse();
    }

    // A cache entry can outlive its package files (e.g. after `dotnet nuget locals global-packages --clear`);
    // `dotnet tool run` would refuse to run it, so the probe must not count it as installed.
    [Test]
    public async Task IsInstalled_WithDanglingExecutablePath_ReturnsFalse()
    {
        using var home = new TempHome();
        WriteCacheFile(home, CacheText("2.1.40-preview", Path.Combine(home.RootPath, "no-such-file.dll")));
        var probe = new ToolResolverCacheProbe(CacheDirectory(home));

        await Assert.That(probe.IsInstalled("bv", Pin)).IsFalse();
    }

    [Test]
    [Arguments("this is not JSON")]
    [Arguments("""{ "Version": "2.1.40-preview" }""")]
    [Arguments("[ 42 ]")]
    public async Task IsInstalled_WithUnrecognizableCacheContent_ReturnsFalse(string content)
    {
        using var home = new TempHome();
        WriteCacheFile(home, content);
        var probe = new ToolResolverCacheProbe(CacheDirectory(home));

        await Assert.That(probe.IsInstalled("bv", Pin)).IsFalse();
    }

    // Reads process-global environment state.
    [Test]
    [NotInParallel]
    public async Task GetDefaultCacheDirectory_HonorsDotnetCliHome()
    {
        var original = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        var cliHome = Path.Combine(Path.GetTempPath(), "bv-test-cli-home");
        Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", cliHome);
        try
        {
            var expected = Path.Combine(cliHome, ".dotnet", "toolResolverCache");
            await Assert.That(ToolResolverCacheProbe.GetDefaultCacheDirectory()).IsEqualTo(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", original);
        }
    }

    private static string CacheDirectory(TempHome home) => Path.Combine(home.RootPath, "toolResolverCache");

    private static void WriteCacheFile(TempHome home, string content)
    {
        var directory = Path.Combine(CacheDirectory(home), "1");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "bv"), content);
    }

    private static string CreateExecutableFile(TempHome home)
    {
        var path = Path.Combine(home.RootPath, "tools", "Buildvana.Tool.dll");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    // The real cache row shape; the executable path goes through the JSON serializer because Windows paths
    // carry backslashes.
    private static string CacheText(string version, string executablePath) => $$"""
        [
          {
            "Version": "{{version}}",
            "TargetFramework": "net10.0",
            "RuntimeIdentifier": "any",
            "Name": "bv",
            "Runner": "dotnet",
            "PathToExecutable": {{JsonSerializer.Serialize(executablePath)}}
          }
        ]
        """;
}
