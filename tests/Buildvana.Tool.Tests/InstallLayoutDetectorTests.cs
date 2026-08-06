// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Infrastructure.Delegation;

// Each case is written with '/' separators, which every platform splits on, and asserted both as-is and
// converted to the native separator — so Windows also covers the '\' form, while on other platforms the
// two forms coincide.
internal sealed class InstallLayoutDetectorTests
{
    private const string Version = "2.1.41-preview";

    [Test]
    [Arguments("/home/ric/.dotnet/tools/.store/bv/2.1.41-preview/bv/2.1.41-preview/tools/net10.0/any/")]
    [Arguments("/custom-tool-path/.store/bv/2.1.41-preview/bv/2.1.41-preview/tools/net10.0/any/")]
    [Arguments("/HOME/RIC/.DOTNET/TOOLS/.STORE/BV/2.1.41-PREVIEW/BV/2.1.41-PREVIEW/TOOLS/NET10.0/ANY/")]
    public async Task Detect_WithToolStorePath_ReturnsToolStore(string baseDirectory)
    {
        await Assert.That(InstallLayoutDetector.Detect(baseDirectory, "bv", Version)).IsEqualTo(InstallLayout.ToolStore);
        await Assert.That(InstallLayoutDetector.Detect(Native(baseDirectory), "bv", Version)).IsEqualTo(InstallLayout.ToolStore);
    }

    [Test]
    [Arguments("/home/ric/.nuget/packages/bv/2.1.41-preview/tools/net10.0/any/")]
    [Arguments("/relocated-package-cache/bv/2.1.41-preview/tools/net10.0/any/")]
    [Arguments("/home/ric/.nuget/packages/bv/2.1.41-preview/tools/net9.0/win-x64/")]
    public async Task Detect_WithPackageCachePath_ReturnsPackageCache(string baseDirectory)
    {
        await Assert.That(InstallLayoutDetector.Detect(baseDirectory, "bv", Version)).IsEqualTo(InstallLayout.PackageCache);
        await Assert.That(InstallLayoutDetector.Detect(Native(baseDirectory), "bv", Version)).IsEqualTo(InstallLayout.PackageCache);
    }

    // The store layout ends with the same <id>/<version>/tools/<tfm>/<rid> suffix as the package layout;
    // the .store segment must win.
    [Test]
    public async Task Detect_WithToolStorePath_DoesNotMistakeItForPackageCache()
    {
        const string baseDirectory = "/home/ric/.dotnet/tools/.store/bv/2.1.41-preview/bv/2.1.41-preview/tools/net10.0/any/";
        await Assert.That(InstallLayoutDetector.Detect(baseDirectory, "bv", Version)).IsNotEqualTo(InstallLayout.PackageCache);
    }

    // A `.store` directory that is not followed by the package ID and version (e.g. part of an unrelated
    // user path) does not make the layout a tool store.
    [Test]
    public async Task Detect_WithUnrelatedStoreSegment_StillRecognizesPackageCache()
    {
        const string baseDirectory = "/work/.store/package-cache/bv/2.1.41-preview/tools/net10.0/any/";
        await Assert.That(InstallLayoutDetector.Detect(baseDirectory, "bv", Version)).IsEqualTo(InstallLayout.PackageCache);
    }

    [Test]
    [Arguments("/projects/buildvana/src/Buildvana.Tool/bin/Debug/net10.0/")]
    [Arguments("/home/ric/.nuget/packages/bv/2.1.40-preview/tools/net10.0/any/")]
    [Arguments("/home/ric/.nuget/packages/some-other-tool/2.1.41-preview/tools/net10.0/any/")]
    [Arguments("/home/ric/.dotnet/tools/.store/bv/2.1.40-preview/bv/2.1.40-preview/tools/net10.0/any/")]
    [Arguments("/bv/")]
    public async Task Detect_WithUnrecognizedOrMismatchedPath_ReturnsUnknown(string baseDirectory)
    {
        await Assert.That(InstallLayoutDetector.Detect(baseDirectory, "bv", Version)).IsEqualTo(InstallLayout.Unknown);
        await Assert.That(InstallLayoutDetector.Detect(Native(baseDirectory), "bv", Version)).IsEqualTo(InstallLayout.Unknown);
    }

    private static string Native(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
