// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real BeforeModules.props, BeforeModules.targets, and Module.targets of the XmlDocumentation
// module in a Microsoft.NET.Sdk project of a temporary home directory. Directory.Build.props imports the
// props file, and Directory.Build.targets imports the two targets files, so that the .NET SDK reads
// GenerateDocumentationFile between them, as it does in a build. Evaluation-only: no targets are executed,
// so assertions are limited to properties.
internal sealed class XmlDocumentationModuleTests
{
    [Test]
    public async Task Evaluate_Library_GeneratesDocumentationFile()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Library", string.Empty);
        await Assert.That(result.XmlDocs).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).EndsWith("Test.xml");
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
    }

    [Test]
    public async Task Evaluate_Exe_SuppressesDocumentationWarnings()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Exe", string.Empty);
        await Assert.That(result.XmlDocs).IsEqualTo("false");
        await Assert.That(result.DocumentationFile).IsEmpty();
        await Assert.That(result.NoWarn).Contains("SA0001");
    }

    // BeforeModules.props sets GenerateDocumentationFile to true before the project, so a project that sets it
    // to false turns the file off for the .NET SDK. XmlDocs used to default to true for a library all the same,
    // so nothing suppressed the documentation rules, and StyleCop reported SA0001.
    [Test]
    public async Task Evaluate_LibraryWithGenerateDocumentationFileOff_SuppressesDocumentationWarnings()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Library", "<GenerateDocumentationFile>false</GenerateDocumentationFile>");
        await Assert.That(result.XmlDocs).IsEqualTo("false");
        await Assert.That(result.DocumentationFile).IsEmpty();
        await Assert.That(result.NoWarn).Contains("SA0001");
    }

    [Test]
    public async Task Evaluate_ExeWithXmlDocsOn_GeneratesDocumentationFile()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Exe", "<XmlDocs>true</XmlDocs>");
        await Assert.That(result.XmlDocs).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).EndsWith("Test.xml");
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
    }

    private static (string XmlDocs, string DocumentationFile, string NoWarn) Evaluate(
        TempHome home,
        string outputType,
        string properties)
    {
        var propsText = $"""
            <Project>
              <Import Project="{GetRealPath("RealXmlDocumentationBeforeModulesPropsPath")}" />
            </Project>
            """;
        home.WriteFile("Directory.Build.props", propsText);
        var targetsText = $"""
            <Project>
              <PropertyGroup>
                <BV_IsLibraryProject>false</BV_IsLibraryProject>
                <BV_IsLibraryProject Condition="'$(OutputType)' == 'Library'">true</BV_IsLibraryProject>
              </PropertyGroup>
              <Import Project="{GetRealPath("RealXmlDocumentationBeforeModulesTargetsPath")}" />
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
            project.GetPropertyValue("XmlDocs"),
            project.GetPropertyValue("DocumentationFile"),
            project.GetPropertyValue("NoWarn"));
    }

    private static string GetRealPath(string key)
        => typeof(XmlDocumentationModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == key)
            .Value!;
}
