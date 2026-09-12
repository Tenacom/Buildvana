// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real InitializePublishFoldersMetadata and ProcessPublishFoldersMetadata targets of the
// AlternatePack module against a temporary project, and reads the PublishFolder metadata back after the
// build. The project states the artifacts directory, the configuration, and the informational version the
// zip file name embeds. ProcessPublishFoldersMetadata used to copy PublishDir into the property it read
// Temporary from, so every folder came out as not temporary, and UniqueZipFileName used to default to false
// whatever the count of zipped folders, so two zipped folders with default names shared one zip path.
// One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class PublishToFoldersTargetsTests
{
    private const string Version = "1.2.3";

    [Test]
    public async Task ProcessPublishFoldersMetadata_WithTemporaryTrue_KeepsTemporaryTrue()
    {
        const string items = """
            <PublishFolder Include="Portable" Temporary="true" />
            <PublishFolder Include="Installer" />
            """;
        var result = Run(items);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Items["Portable"].GetMetadataValue("Temporary")).IsEqualTo("true");
        await Assert.That(result.Items["Installer"].GetMetadataValue("Temporary")).IsEqualTo("false");
    }

    [Test]
    public async Task ProcessPublishFoldersMetadata_WithOneZippedFolder_UsesThePlainZipFileName()
    {
        const string items = """
            <PublishFolder Include="Portable" CreateZipFile="true" />
            <PublishFolder Include="Installer" />
            """;
        var result = Run(items);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Items["Portable"].GetMetadataValue("UniqueZipFileName")).IsEqualTo("false");
        await Assert.That(result.Items["Portable"].GetMetadataValue("ZipFileName")).IsEqualTo($"Test_{Version}.zip");
        await Assert.That(result.Items["Installer"].GetMetadataValue("CreateZipFile")).IsEqualTo("false");
    }

    [Test]
    public async Task ProcessPublishFoldersMetadata_WithTwoZippedFolders_AddsTheIdentityToTheZipFileName()
    {
        const string items = """
            <PublishFolder Include="Portable" CreateZipFile="true" />
            <PublishFolder Include="Extra" ZipFileName="extra.zip" />
            """;
        var result = Run(items);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Items["Portable"].GetMetadataValue("UniqueZipFileName")).IsEqualTo("true");
        await Assert.That(result.Items["Portable"].GetMetadataValue("ZipFileName")).IsEqualTo($"Test-Portable_{Version}.zip");
        await Assert.That(result.Items["Extra"].GetMetadataValue("UniqueZipFileName")).IsEqualTo("true");
        await Assert.That(result.Items["Extra"].GetMetadataValue("ZipFileName")).IsEqualTo("extra.zip");
    }

    [Test]
    public async Task ProcessPublishFoldersMetadata_WithUniqueZipFileNameStated_KeepsIt()
    {
        const string items = """
            <PublishFolder Include="Portable" CreateZipFile="true" UniqueZipFileName="false" />
            <PublishFolder Include="Extra" ZipFileName="extra.zip" />
            """;
        var result = Run(items);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Items["Portable"].GetMetadataValue("UniqueZipFileName")).IsEqualTo("false");
        await Assert.That(result.Items["Portable"].GetMetadataValue("ZipFileName")).IsEqualTo($"Test_{Version}.zip");
    }

    private static (bool Succeeded, Dictionary<string, ProjectItemInstance> Items) Run(string items)
    {
        using var home = new TempHome();
        var artifactsDirectory = home.GetFullPath("artifacts") + Path.DirectorySeparatorChar;
        var projectPath = home.GetFullPath("Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <ArtifactsDirectory>{artifactsDirectory}</ArtifactsDirectory>
                <Configuration>Release</Configuration>
                <AssemblyInformationalVersion>{Version}</AssemblyInformationalVersion>
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
        var request = new BuildRequestData(
            projectPath,
            globalProperties,
            null,
            ["InitializePublishFoldersMetadata", "ProcessPublishFoldersMetadata"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        var publishFolders = result.ProjectStateAfterBuild?.GetItems("PublishFolder") ?? [];
        return (
            result.OverallResult == BuildResultCode.Success,
            publishFolders.ToDictionary(static item => item.EvaluatedInclude));
    }

    private static string GetRealTargetsPath()
        => typeof(PublishToFoldersTargetsTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealAlternatePackPublishToFoldersTargetsPath")
            .Value!;
}
