// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real BeforeNETSdk.targets and the XmlDocumentation module in a Microsoft.NET.Sdk project of a
// temporary home directory. Directory.Build.props names BeforeNETSdk.targets in BeforeMicrosoftNETSdkTargets and
// points BuildvanaModulesDirectory at the real modules, so the .NET SDK imports the module's
// Module.BeforeNETSdk.targets right after the project body. Directory.Build.targets imports the module's
// Module.targets, which runs after the .NET SDK has read GenerateDocumentationFile, as in a build.
// Evaluation-only: no targets are executed, so assertions are limited to properties.
internal sealed class XmlDocumentationModuleTests
{
    [Test]
    public async Task Evaluate_Library_GeneratesDocumentationFile()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Library", string.Empty);
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).EndsWith("Test.xml");
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
    }

    [Test]
    public async Task Evaluate_Exe_SuppressesDocumentationWarnings()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Exe", string.Empty);
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("false");
        await Assert.That(result.DocumentationFile).IsEmpty();
        await Assert.That(result.NoWarn).Contains("SA0001");
    }

    // The module used to read XmlDocs, default it to true for a library, and force GenerateDocumentationFile to
    // true before the project. A library that set GenerateDocumentationFile back to false got no file from the
    // .NET SDK, and no suppression from the module, so StyleCop reported SA0001.
    [Test]
    public async Task Evaluate_LibraryWithGenerateDocumentationFileOff_SuppressesDocumentationWarnings()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Library", "<GenerateDocumentationFile>false</GenerateDocumentationFile>");
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("false");
        await Assert.That(result.DocumentationFile).IsEmpty();
        await Assert.That(result.NoWarn).Contains("SA0001");
    }

    // With XmlDocs at its default of false for an Exe, the module used to set GenerateDocumentationFile back to
    // false, so an Exe that set the property got no file.
    [Test]
    public async Task Evaluate_ExeWithGenerateDocumentationFileOn_GeneratesDocumentationFile()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Exe", "<GenerateDocumentationFile>true</GenerateDocumentationFile>");
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).EndsWith("Test.xml");
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
    }

    // The .NET SDK turns GenerateDocumentationFile on when the project sets DocumentationFile alone, and the module
    // leaves that to it.
    [Test]
    public async Task Evaluate_ExeWithDocumentationFileAlone_GeneratesDocumentationFile()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Exe", "<DocumentationFile>docs/Test.xml</DocumentationFile>");
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).IsEqualTo("docs/Test.xml");
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
    }

    private static (string GenerateDocumentationFile, string DocumentationFile, string NoWarn) Evaluate(
        TempHome home,
        string outputType,
        string properties)
    {
        var propsText = $"""
            <Project>
              <PropertyGroup>
                <BeforeMicrosoftNETSdkTargets>{GetRealPath("RealBeforeNETSdkTargetsPath")}</BeforeMicrosoftNETSdkTargets>
                <BuildvanaModulesDirectory>{GetRealPath("RealModulesDirectory")}</BuildvanaModulesDirectory>
              </PropertyGroup>
            </Project>
            """;
        home.WriteFile("Directory.Build.props", propsText);
        var targetsText = $"""
            <Project>
              <Import Project="{GetRealPath("RealXmlDocumentationModuleTargetsPath")}" />
            </Project>
            """;
        home.WriteFile("Directory.Build.targets", targetsText);
        var projectPath = home.GetFullPath("src/Test/Test.csproj");
        var projectText = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>{outputType}</OutputType>
                {properties}
              </PropertyGroup>
            </Project>
            """;
        home.WriteFile("src/Test/Test.csproj", projectText);

        using var collection = new ProjectCollection();
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        return (
            project.GetPropertyValue("GenerateDocumentationFile"),
            project.GetPropertyValue("DocumentationFile"),
            project.GetPropertyValue("NoWarn"));
    }

    private static string GetRealPath(string key)
        => typeof(XmlDocumentationModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == key)
            .Value!;
}
