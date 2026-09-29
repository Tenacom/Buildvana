// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Module.Core.targets of the AdditionalAssemblyInfo module, imported from Directory.Build.targets,
// in a Microsoft.NET.Sdk project of a temporary home directory. Module.targets imports the file in a real build when
// GenerateAdditionalAssemblyInfo is true. AdditionalAssemblyInfoGenerator reads CLSCompliant and ComVisible as the
// literal 'true', so the module must store that literal for every value MSBuild compares equal to it.
// Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class AdditionalAssemblyInfoModuleTests
{
    private const string ProjectDirectory = "src/Test";

    [Test]
    [Arguments("", "true")]
    [Arguments("true", "true")]
    [Arguments("yes", "true")]
    [Arguments("On", "true")]
    [Arguments("!false", "true")]
    [Arguments("false", "false")]
    [Arguments("no", "false")]
    [Arguments("maybe", "false")]
    public async Task Evaluate_CLSCompliant_StoresTheLiteral(string value, string expected)
    {
        using var home = new TempHome();
        var result = Evaluate(home, $"<CLSCompliant>{value}</CLSCompliant>", "CLSCompliant");
        await Assert.That(result.Value).IsEqualTo(expected);
    }

    [Test]
    [Arguments("", "false")]
    [Arguments("true", "true")]
    [Arguments("yes", "true")]
    [Arguments("On", "true")]
    [Arguments("!false", "true")]
    [Arguments("false", "false")]
    [Arguments("no", "false")]
    [Arguments("maybe", "false")]
    public async Task Evaluate_ComVisible_StoresTheLiteral(string value, string expected)
    {
        using var home = new TempHome();
        var result = Evaluate(home, $"<ComVisible>{value}</ComVisible>", "ComVisible");
        await Assert.That(result.Value).IsEqualTo(expected);
    }

    [Test]
    [Arguments("GenerateAssemblyCLSCompliantAttribute", "CLSCompliant")]
    [Arguments("GenerateAssemblyComVisibleAttribute", "ComVisible")]
    public async Task Evaluate_GenerateAttributeYes_StoresTrueAndExposesTheProperty(
        string switchName,
        string propertyName)
    {
        using var home = new TempHome();
        var result = Evaluate(home, $"<{switchName}>yes</{switchName}>", switchName);
        await Assert.That(result.Value).IsEqualTo("true");
        await Assert.That(result.CompilerVisibleProperties).Contains(propertyName);
    }

    [Test]
    [Arguments("GenerateAssemblyCLSCompliantAttribute", "CLSCompliant")]
    [Arguments("GenerateAssemblyComVisibleAttribute", "ComVisible")]
    public async Task Evaluate_GenerateAttributeNo_StoresFalseAndHidesTheProperty(
        string switchName,
        string propertyName)
    {
        using var home = new TempHome();
        var result = Evaluate(home, $"<{switchName}>no</{switchName}>", switchName);
        await Assert.That(result.Value).IsEqualTo("false");
        await Assert.That(result.CompilerVisibleProperties).DoesNotContain(propertyName);
    }

    private static (string Value, string[] CompilerVisibleProperties) Evaluate(
        TempHome home,
        string properties,
        string propertyName)
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
                <TargetFramework>net10.0</TargetFramework>
                {properties}
              </PropertyGroup>
            </Project>
            """;
        home.WriteFile($"{ProjectDirectory}/Test.csproj", projectText);

        using var collection = new ProjectCollection();
        var options = new ProjectOptions { ProjectCollection = collection };
        var project = Project.FromFile(projectPath, options);
        return (
            project.GetPropertyValue(propertyName),
            [.. project.GetItems("CompilerVisibleProperty").Select(static item => item.EvaluatedInclude)]);
    }

    private static string GetRealTargetsPath()
        => typeof(AdditionalAssemblyInfoModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealAdditionalAssemblyInfoModuleCoreTargetsPath")
            .Value!;
}
