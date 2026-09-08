// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real InitializeInnoSetupMetadata and CompleteInnoSetupMetadata targets of the AlternatePack
// module against a temporary project, and reads the InnoSetup metadata back after the build. The project
// states AppShortName, which InitializeInnoSetupProperties would default, the informational version the
// output name embeds, and the artifacts directory OutputDir resolves against. CompleteInnoSetupMetadata
// used to count the InnoSetup items itself, but it is batched over them, so the count was always 1, and
// its OutputName default read UniqueOutputName before the default was written, so a single item came out
// as MyApp-Main_1.2.3.
// One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class InnoSetupTargetsTests
{
    private const string AppShortName = "MyApp";
    private const string Version = "1.2.3";

    [Test]
    public async Task CompleteInnoSetupMetadata_WithOneItem_UsesThePlainOutputName()
    {
        const string items = """
            <InnoSetup Include="Main" Script="main.iss" />
            """;
        var result = Run(items);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Items["Main"].GetMetadataValue("UniqueOutputName")).IsEqualTo("false");
        await Assert.That(result.Items["Main"].GetMetadataValue("OutputName")).IsEqualTo($"{AppShortName}_{Version}");
    }

    [Test]
    public async Task CompleteInnoSetupMetadata_WithTwoItems_AddsTheIdentityToTheOutputName()
    {
        const string items = """
            <InnoSetup Include="Main" Script="main.iss" />
            <InnoSetup Include="Lite" Script="lite.iss" />
            """;
        var result = Run(items);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Items["Main"].GetMetadataValue("UniqueOutputName")).IsEqualTo("true");
        await Assert.That(result.Items["Main"].GetMetadataValue("OutputName")).IsEqualTo($"{AppShortName}-Main_{Version}");
        await Assert.That(result.Items["Lite"].GetMetadataValue("UniqueOutputName")).IsEqualTo("true");
        await Assert.That(result.Items["Lite"].GetMetadataValue("OutputName")).IsEqualTo($"{AppShortName}-Lite_{Version}");
    }

    [Test]
    public async Task CompleteInnoSetupMetadata_WithUniqueOutputNameStated_KeepsIt()
    {
        const string items = """
            <InnoSetup Include="Main" Script="main.iss" />
            <InnoSetup Include="Lite" Script="lite.iss" UniqueOutputName="false" />
            """;
        var result = Run(items);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Items["Main"].GetMetadataValue("UniqueOutputName")).IsEqualTo("true");
        await Assert.That(result.Items["Lite"].GetMetadataValue("UniqueOutputName")).IsEqualTo("false");
        await Assert.That(result.Items["Lite"].GetMetadataValue("OutputName")).IsEqualTo($"{AppShortName}_{Version}");
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
                <AppShortName>{AppShortName}</AppShortName>
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
            ["InitializeInnoSetupMetadata", "CompleteInnoSetupMetadata"],
            null,
            BuildRequestDataFlags.ProvideProjectStateAfterBuild);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        var innoSetups = result.ProjectStateAfterBuild?.GetItems("InnoSetup") ?? [];
        return (
            result.OverallResult == BuildResultCode.Success,
            innoSetups.ToDictionary(static item => item.EvaluatedInclude));
    }

    private static string GetRealTargetsPath()
        => typeof(InnoSetupTargetsTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealAlternatePackInnoSetupTargetsPath")
            .Value!;
}
