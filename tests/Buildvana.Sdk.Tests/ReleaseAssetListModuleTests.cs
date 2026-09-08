// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real targets of the ReleaseAssetList module against a temporary project, and reads the list file
// back. The project states the two project-type properties that Sdk.targets computes in a real build, and the
// artifacts directory. The module used to default the description of an asset to "(no description given)",
// which bv release then set as the GitHub label of the asset, shown in place of its file name.
// One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class ReleaseAssetListModuleTests
{
    private const string ListFileName = "Test.assets.txt";

    [Test]
    public async Task WriteReleaseAssetList_WithTwoItems_WritesOneLinePerItem()
    {
        using var home = new TempHome();
        const string items = """
            <ReleaseAsset Include="first.bin" Description="  First asset  " />
            <ReleaseAsset Include="second.zip" MimeType="application/zip" />
            """;
        var result = Run(home, items, defaultDescription: null);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Lines.Length).IsEqualTo(2);
        await Assert.That(result.Lines[0]).IsEqualTo($"{home.GetFullPath("first.bin")}\tapplication/octet-stream\tFirst asset");
        await Assert.That(result.Lines[1]).IsEqualTo($"{home.GetFullPath("second.zip")}\tapplication/zip\t");
    }

    [Test]
    public async Task WriteReleaseAssetList_WithDefaultDescription_AppliesItToAnItemWithNone()
    {
        using var home = new TempHome();
        const string items = """
            <ReleaseAsset Include="second.zip" MimeType="application/zip" />
            """;
        var result = Run(home, items, defaultDescription: "See the release notes");
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Lines.Length).IsEqualTo(1);
        await Assert.That(result.Lines[0]).IsEqualTo($"{home.GetFullPath("second.zip")}\tapplication/zip\tSee the release notes");
    }

    [Test]
    public async Task WriteReleaseAssetList_WithNoItem_DeletesTheFile()
    {
        using var home = new TempHome();
        home.WriteFile("artifacts/Release/" + ListFileName, "stale");
        var result = Run(home, string.Empty, defaultDescription: null);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.ListExists).IsFalse();
    }

    private static (bool Succeeded, bool ListExists, string[] Lines) Run(
        TempHome home,
        string items,
        string? defaultDescription)
    {
        var artifactsDirectory = home.GetFullPath("artifacts") + Path.DirectorySeparatorChar;
        var defaultDescriptionProperty = defaultDescription is null
            ? string.Empty
            : $"<ReleaseAssetDefaultDescription>{defaultDescription}</ReleaseAssetDefaultDescription>";
        var projectPath = home.GetFullPath("Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <BV_IsLibraryProject>false</BV_IsLibraryProject>
                <BV_IsTestProject>false</BV_IsTestProject>
                <ArtifactsDirectory>{artifactsDirectory}</ArtifactsDirectory>
                <Configuration>Release</Configuration>
                {defaultDescriptionProperty}
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
              <Import Project="{GetRealTargetsPath()}" />
            </Project>
            """;
        home.WriteFile("Test.proj", projectText);

        var globalProperties = new Dictionary<string, string?>();
        using var collection = new ProjectCollection(globalProperties);
        var logger = new RecordingMSBuildLogger();
        var parameters = new BuildParameters(collection) { Loggers = [logger] };
        var request = new BuildRequestData(projectPath, globalProperties, null, ["WriteReleaseAssetList"], null);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        var listPath = Path.Combine(artifactsDirectory, "Release", ListFileName);
        var listExists = File.Exists(listPath);
        string[] lines = listExists ? File.ReadAllLines(listPath) : [];
        return (result.OverallResult == BuildResultCode.Success, listExists, lines);
    }

    private static string GetRealTargetsPath()
        => typeof(ReleaseAssetListModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealReleaseAssetListModuleTargetsPath")
            .Value!;
}
