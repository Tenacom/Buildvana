// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real BV_CheckRoslynVersion target of the SourceGenerators module against a temporary project.
// The project states the language, the compiler version, and the floor itself, and stubs
// ResolvePackageAssets, which the target depends on. The two Error elements of the target used to sit
// inside an ItemGroup, where MSBuild reads them as item operations, so neither diagnostic ever fired.
// One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class CheckRoslynVersionTests
{
    private const string Floor = "5.9";
    private const string Hint = ".NET SDK 10.0.4xx / Visual Studio 2026 18.9+";

    [Test]
    public async Task Target_RoslynBelowTheFloor_RaisesBVSDK1101()
    {
        var (succeeded, errors) = Run("C#", "roslyn5.8");
        await Assert.That(succeeded).IsFalse();
        await Assert.That(errors).IsEquivalentTo(
            [$"BVSDK1101: Buildvana SDK source generators require Roslyn version {Floor} or later ({Hint})"]);
    }

    [Test]
    public async Task Target_RoslynAtTheFloor_Succeeds()
    {
        var (succeeded, errors) = Run("C#", "roslyn" + Floor);
        await Assert.That(succeeded).IsTrue();
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task Target_LanguageOtherThanCSharp_RaisesBVSDK1100()
    {
        var (succeeded, errors) = Run("F#", "roslyn" + Floor);
        await Assert.That(succeeded).IsFalse();
        await Assert.That(errors).IsEquivalentTo(
            ["BVSDK1100: Buildvana SDK source generators are only supported in C# projects."]);
    }

    private static (bool Succeeded, IReadOnlyList<string> Errors) Run(string language, string compilerApiVersion)
    {
        using var home = new TempHome();
        var projectPath = home.GetFullPath("Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <Language>{language}</Language>
                <CompilerApiVersion>{compilerApiVersion}</CompilerApiVersion>
                <BV_MinRoslynVersion>{Floor}</BV_MinRoslynVersion>
                <BV_MinRoslynVersionHint>{Hint}</BV_MinRoslynVersionHint>
              </PropertyGroup>
              <Target Name="ResolvePackageAssets" />
              <Import Project="{GetRealTargetsPath()}" />
            </Project>
            """;
        home.WriteFile("Test.proj", projectText);

        var globalProperties = new Dictionary<string, string?>();
        using var collection = new ProjectCollection(globalProperties);
        var logger = new RecordingMSBuildLogger();
        var parameters = new BuildParameters(collection) { Loggers = [logger] };
        var request = new BuildRequestData(projectPath, globalProperties, null, ["BV_CheckRoslynVersion"], null);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        return (result.OverallResult == BuildResultCode.Success, logger.Errors);
    }

    private static string GetRealTargetsPath()
        => typeof(CheckRoslynVersionTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealSourceGeneratorsAfterModulesCoreTargetsPath")
            .Value!;
}
