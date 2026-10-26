// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Module.targets of the StandardAnalyzers module, imported by a project of a temporary home
// directory that states the project type properties Buildvana SDK computes before the module runs.
// Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class StandardAnalyzersModuleTargetsTests
{
    private const string NullableExtendedAnalyzer = "Nullable.Extended.Analyzer";

    [Test]
    public async Task Evaluate_ByDefault_ReferencesNullableExtendedAnalyzer()
    {
        using var home = new TempHome();
        var result = Evaluate(home, isNoTargetsProject: false, string.Empty);
        await Assert.That(result.UseNullableExtendedAnalyzer).IsEqualTo("true");
        await Assert.That(result.PrivateAssets).IsEqualTo("All");
    }

    [Test]
    public async Task Evaluate_NoTargetsProject_LeavesNullableExtendedAnalyzerOut()
    {
        using var home = new TempHome();
        var result = Evaluate(home, isNoTargetsProject: true, string.Empty);
        await Assert.That(result.UseNullableExtendedAnalyzer).IsEqualTo("false");
        await Assert.That(result.PrivateAssets).IsNull();
    }

    // A no-target project defaults the property to false, so a value that stores 'true' is the one set.
    [Test]
    [Arguments("true")]
    [Arguments("yes")]
    [Arguments("On")]
    [Arguments("!false")]
    public async Task Evaluate_ValueMSBuildReadsAsTrue_StoresTrue(string value)
    {
        using var home = new TempHome();
        var result = Evaluate(home, isNoTargetsProject: true, value);
        await Assert.That(result.UseNullableExtendedAnalyzer).IsEqualTo("true");
        await Assert.That(result.PrivateAssets).IsEqualTo("All");
    }

    [Test]
    [Arguments("false")]
    [Arguments("no")]
    [Arguments("maybe")]
    public async Task Evaluate_ValueOtherThanTrue_StoresFalse(string value)
    {
        using var home = new TempHome();
        var result = Evaluate(home, isNoTargetsProject: false, value);
        await Assert.That(result.UseNullableExtendedAnalyzer).IsEqualTo("false");
        await Assert.That(result.PrivateAssets).IsNull();
    }

    // The PrivateAssets metadata of the module's reference to Nullable.Extended.Analyzer, or null when the module
    // adds no reference.
    private static (string UseNullableExtendedAnalyzer, string? PrivateAssets) Evaluate(
        TempHome home,
        bool isNoTargetsProject,
        string useNullableExtendedAnalyzer)
    {
        var projectPath = home.GetFullPath("Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <HomeDirectory>{home.RootPath}{Path.DirectorySeparatorChar}</HomeDirectory>
                <BV_IsNoTargetsProject>{(isNoTargetsProject ? "true" : "false")}</BV_IsNoTargetsProject>
                <BV_IsLibraryProject>false</BV_IsLibraryProject>
                <UseNullableExtendedAnalyzer>{useNullableExtendedAnalyzer}</UseNullableExtendedAnalyzer>
              </PropertyGroup>
              <Import Project="{GetRealTargetsPath()}" />
            </Project>
            """;
        home.WriteFile("Test.proj", projectText);

        using var collection = new ProjectCollection();
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        var reference = project.GetItems("BV_PackageReference")
            .FirstOrDefault(static item => item.EvaluatedInclude == NullableExtendedAnalyzer);
        return (project.GetPropertyValue("UseNullableExtendedAnalyzer"), reference?.GetMetadataValue("PrivateAssets"));
    }

    private static string GetRealTargetsPath()
        => typeof(StandardAnalyzersModuleTargetsTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealStandardAnalyzersModuleTargetsPath")
            .Value!;
}
