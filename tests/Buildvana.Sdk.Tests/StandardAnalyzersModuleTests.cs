// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Module.UsePublicApiAnalyzers.targets of the StandardAnalyzers module, imported from
// Directory.Build.targets, in a Microsoft.NET.Sdk project of a temporary home directory. Module.targets imports
// the file in a real build when UsePublicApiAnalyzers is true. In a build, Microsoft.CodeAnalysis.PublicApiAnalyzers
// adds PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt of the project directory to AdditionalFiles, and a project
// that lists the two files stands in for it here. A multi-targeting project is evaluated as the inner build for
// net10.0, unless a test reads the outer build.
// Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class StandardAnalyzersModuleTests
{
    private const string ProjectDirectory = "src/Test";
    private const string SingleTarget = "<TargetFramework>net10.0</TargetFramework>";
    private const string MultiTarget = "<TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>";
    private const string InnerBuild = "net10.0";
    private const string ListedPublicApiFiles = """
        <AdditionalFiles Include="PublicAPI.Shipped.txt" />
        <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
        """;

    [Test]
    public async Task Evaluate_SingleTarget_DefaultsBothLayoutsToFlat()
    {
        using var home = new TempHome();
        var result = Evaluate(home, SingleTarget, string.Empty, null);
        await Assert.That(result.PublicLayout).IsEqualTo("false");
        await Assert.That(result.InternalLayout).IsEqualTo("false");
    }

    [Test]
    public async Task Evaluate_MultiTarget_DefaultsBothLayoutsToTfmSpecific()
    {
        using var home = new TempHome();
        var result = Evaluate(home, MultiTarget, string.Empty, null);
        await Assert.That(result.PublicLayout).IsEqualTo("true");
        await Assert.That(result.InternalLayout).IsEqualTo("true");
    }

    [Test]
    public async Task Evaluate_InternalLayoutOtherThanTrue_CountsAsFalse()
    {
        using var home = new TempHome();
        var properties = MultiTarget + "<UseTfmSpecificInternalApiFiles>maybe</UseTfmSpecificInternalApiFiles>";
        var result = Evaluate(home, properties, string.Empty, null);
        await Assert.That(result.InternalLayout).IsEqualTo("false");
    }

    [Test]
    public async Task Evaluate_FlatInternalLayout_AddsTheInternalApiFiles()
    {
        using var home = new TempHome();
        WritePair(home, ProjectDirectory, "InternalAPI");
        var result = Evaluate(home, SingleTarget, string.Empty, null);
        await Assert.That(result.AdditionalFiles).IsEquivalentTo(["InternalAPI.Shipped.txt", "InternalAPI.Unshipped.txt"]);
    }

    [Test]
    public async Task Evaluate_FlatInternalLayoutWithShippedFileAlone_AddsTheShippedFile()
    {
        using var home = new TempHome();
        home.WriteFile($"{ProjectDirectory}/InternalAPI.Shipped.txt", "#nullable enable\n");
        var result = Evaluate(home, SingleTarget, string.Empty, null);
        await Assert.That(result.AdditionalFiles).IsEquivalentTo(["InternalAPI.Shipped.txt"]);
    }

    [Test]
    public async Task Evaluate_FlatInternalLayoutWithFileListedByTheProject_AddsTheFileOnce()
    {
        using var home = new TempHome();
        WritePair(home, ProjectDirectory, "InternalAPI");
        const string items = """<AdditionalFiles Include="InternalAPI.Shipped.txt" />""";
        var result = Evaluate(home, SingleTarget, items, null);
        await Assert.That(result.AdditionalFiles.Count(static path => path == "InternalAPI.Shipped.txt")).IsEqualTo(1);
    }

    [Test]
    public async Task Evaluate_TfmSpecificInternalLayout_AddsTheInternalApiFilesOfTheFramework()
    {
        using var home = new TempHome();
        WritePair(home, $"{ProjectDirectory}/InternalAPI/net10.0", "InternalAPI");
        WritePair(home, $"{ProjectDirectory}/InternalAPI/netstandard2.0", "InternalAPI");
        var result = Evaluate(home, MultiTarget, string.Empty, InnerBuild);
        string[] expected = ["InternalAPI/net10.0/InternalAPI.Shipped.txt", "InternalAPI/net10.0/InternalAPI.Unshipped.txt"];
        await Assert.That(result.AdditionalFiles).IsEquivalentTo(expected);
        await Assert.That(result.None).DoesNotContain(expected[0]);
        await Assert.That(result.None).DoesNotContain(expected[1]);
    }

    [Test]
    public async Task Evaluate_TfmSpecificInternalLayoutWithFileListedByTheProject_AddsTheFileOnce()
    {
        using var home = new TempHome();
        WritePair(home, $"{ProjectDirectory}/InternalAPI/net10.0", "InternalAPI");
        const string path = "InternalAPI/net10.0/InternalAPI.Shipped.txt";
        const string items = $"""<AdditionalFiles Include="{path}" />""";
        var result = Evaluate(home, MultiTarget, items, InnerBuild);
        await Assert.That(result.AdditionalFiles.Count(static item => item == path)).IsEqualTo(1);
    }

    // The two layout properties are independent: a multi-targeting project may keep one internal pair for every
    // framework, and one public pair per framework.
    [Test]
    public async Task Evaluate_MultiTargetWithFlatInternalLayout_KeepsTheTfmSpecificPublicLayout()
    {
        using var home = new TempHome();
        WritePair(home, ProjectDirectory, "InternalAPI");
        WritePair(home, $"{ProjectDirectory}/PublicAPI/net10.0", "PublicAPI");
        var properties = MultiTarget + "<UseTfmSpecificInternalApiFiles>false</UseTfmSpecificInternalApiFiles>";
        var result = Evaluate(home, properties, string.Empty, InnerBuild);
        string[] expected = [
            "InternalAPI.Shipped.txt",
            "InternalAPI.Unshipped.txt",
            "PublicAPI/net10.0/PublicAPI.Shipped.txt",
            "PublicAPI/net10.0/PublicAPI.Unshipped.txt",
        ];
        await Assert.That(result.AdditionalFiles).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Evaluate_FlatPublicLayout_KeepsThePublicApiFilesOfTheProjectDirectory()
    {
        using var home = new TempHome();
        WritePair(home, ProjectDirectory, "PublicAPI");
        var result = Evaluate(home, SingleTarget, ListedPublicApiFiles, null);
        await Assert.That(result.AdditionalFiles).IsEquivalentTo(["PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"]);
    }

    [Test]
    public async Task Evaluate_TfmSpecificPublicLayout_ReplacesThePublicApiFilesOfTheProjectDirectory()
    {
        using var home = new TempHome();
        WritePair(home, ProjectDirectory, "PublicAPI");
        WritePair(home, $"{ProjectDirectory}/PublicAPI/net10.0", "PublicAPI");
        WritePair(home, $"{ProjectDirectory}/PublicAPI/netstandard2.0", "PublicAPI");
        var result = Evaluate(home, MultiTarget, ListedPublicApiFiles, InnerBuild);
        string[] expected = ["PublicAPI/net10.0/PublicAPI.Shipped.txt", "PublicAPI/net10.0/PublicAPI.Unshipped.txt"];
        await Assert.That(result.AdditionalFiles).IsEquivalentTo(expected);
    }

    private static (string PublicLayout, string InternalLayout, string[] AdditionalFiles, string[] None) Evaluate(
        TempHome home,
        string properties,
        string items,
        string? targetFramework)
    {
        var targetsText = $"""
            <Project>
              <Import Project="{GetRealTargetsPath()}" />
            </Project>
            """;
        home.WriteFile("Directory.Build.targets", targetsText);
        var projectPath = home.GetFullPath($"{ProjectDirectory}/Test.csproj");
        var projectText = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {properties}
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
            </Project>
            """;
        home.WriteFile($"{ProjectDirectory}/Test.csproj", projectText);

        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (targetFramework is not null)
        {
            globalProperties["TargetFramework"] = targetFramework;
        }

        using var collection = new ProjectCollection();
        var options = new ProjectOptions { ProjectCollection = collection, GlobalProperties = globalProperties };
        var project = Project.FromFile(projectPath, options);
        return (
            project.GetPropertyValue("UseTfmSpecificPublicApiFiles"),
            project.GetPropertyValue("UseTfmSpecificInternalApiFiles"),
            GetPaths(project, "AdditionalFiles"),
            GetPaths(project, "None"));
    }

    // Item paths with forward slashes, whatever the platform, so that one expected value serves Windows and Linux.
    private static string[] GetPaths(Project project, string itemType)
        => [.. project.GetItems(itemType).Select(static item => item.EvaluatedInclude.Replace('\\', '/'))];

    private static void WritePair(TempHome home, string directory, string prefix)
    {
        home.WriteFile($"{directory}/{prefix}.Shipped.txt", "#nullable enable\n");
        home.WriteFile($"{directory}/{prefix}.Unshipped.txt", "#nullable enable\n");
    }

    private static string GetRealTargetsPath()
        => typeof(StandardAnalyzersModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealStandardAnalyzersUsePublicApiAnalyzersTargetsPath")
            .Value!;
}
