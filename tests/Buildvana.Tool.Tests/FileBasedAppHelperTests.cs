// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Buildvana.Tool.Utilities;

internal sealed class FileBasedAppHelperTests
{
    [Test]
    public async Task GetArtifactsDirectory_IsUnderTheRunfileCacheRoot()
    {
        var root = OperatingSystem.IsWindows()
            ? Path.GetTempPath()
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var expectedParent = Path.Join(root, "dotnet", "runfile");

        var result = FileBasedAppHelper.GetArtifactsDirectory(Path.Combine(Path.GetTempPath(), "app.cs"));

        await Assert.That(result).IsNotNull();
        await Assert.That(Path.IsPathRooted(result)).IsTrue();
        await Assert.That(Path.GetDirectoryName(result)).IsEqualTo(expectedParent);
    }

    [Test]
    public async Task GetArtifactsDirectory_MatchesSdkHashingScheme()
    {
        var (path, expectedName) = OperatingSystem.IsWindows()
            ? (@"C:\hooks\test.cs", "test-7a85c1b24f89716c9af836acc67bf776c1862a1f40f2dae5fdfee77d0a75f2ab")
            : ("/hooks/test.cs", "test-ae55a889b88853cf0eea9153acbee43958029018110e122fa68502a4841342b2");

        var result = FileBasedAppHelper.GetArtifactsDirectory(path);

        await Assert.That(Path.GetFileName(result)).IsEqualTo(expectedName);
    }

    [Test]
    public async Task GetArtifactsDirectory_NormalizesDirectoryCasing()
    {
        var first = Path.Combine(Path.GetTempPath(), "HOOKS", "app.cs");
        var second = Path.Combine(Path.GetTempPath(), "hooks", "app.cs");

        await Assert.That(FileBasedAppHelper.GetArtifactsDirectory(first))
            .IsEqualTo(FileBasedAppHelper.GetArtifactsDirectory(second));
    }

    [Test]
    public async Task GetArtifactsDirectory_DistinguishesDifferentPaths()
    {
        var first = Path.Combine(Path.GetTempPath(), "first", "app.cs");
        var second = Path.Combine(Path.GetTempPath(), "second", "app.cs");

        await Assert.That(FileBasedAppHelper.GetArtifactsDirectory(first)!)
            .IsNotEqualTo(FileBasedAppHelper.GetArtifactsDirectory(second)!);
    }

    [Test]
    public async Task GetArtifactsDirectory_ResolvesRelativePaths()
    {
        var relative = Path.Combine("hooks", "app.cs");
        var absolute = Path.GetFullPath(relative);

        await Assert.That(FileBasedAppHelper.GetArtifactsDirectory(relative))
            .IsEqualTo(FileBasedAppHelper.GetArtifactsDirectory(absolute));
    }

    // Change detector for the accepted risk of computing the path locally: if a future .NET SDK changes
    // its artifacts-path scheme, this test fails while the pure unit tests above keep passing.
    [Test]
    [Timeout(120_000)]
    public async Task GetArtifactsDirectory_AgreesWithInstalledSdk(CancellationToken cancellationToken)
    {
        // A file in the system temp directory sits outside any repository, so evaluating its virtual project
        // involves the machine's .NET SDK alone: no global.json, no Directory.Build.props, no Buildvana SDK.
        var appPath = Path.Combine(Path.GetTempPath(), $"bv-test-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(appPath, "// evaluation-only: never restored, built, or run", cancellationToken).ConfigureAwait(false);
        try
        {
            // --getProperty with no explicit target puts the CLI in evaluation-only mode; `dotnet clean`
            // would instead forward -t:Clean and actually run the target.
            var startInfo = new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "build", appPath, "--getProperty:ArtifactsPath" },
                WorkingDirectory = Path.GetTempPath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(startInfo)!;
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            await Assert.That(process.ExitCode).IsEqualTo(0);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var sdkPath = Path.TrimEndingDirectorySeparator(lines[^1]);
            await Assert.That(sdkPath).IsEqualTo(FileBasedAppHelper.GetArtifactsDirectory(appPath));
        }
        finally
        {
            File.Delete(appPath);
        }
    }
}
