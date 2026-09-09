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
// so assertions are limited to properties and items.
internal sealed partial class XmlDocumentationModuleTests
{
    [Test]
    public async Task Evaluate_Library_GeneratesDocumentationFile()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Library", string.Empty);
        await Assert.That(result.XmlDocs).IsEqualTo("true");
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).EndsWith("Test.xml");
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
        await Assert.That(result.Warnings).IsEmpty();
    }

    [Test]
    public async Task Evaluate_Exe_SuppressesDocumentationWarnings()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Exe", string.Empty);
        await Assert.That(result.XmlDocs).IsEqualTo("false");
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("false");
        await Assert.That(result.DocumentationFile).IsEmpty();
        await Assert.That(result.NoWarn).Contains("SA0001");
        await Assert.That(result.Warnings).IsEmpty();
    }

    // BeforeModules.props sets GenerateDocumentationFile to true before the project, and the .NET SDK empties
    // DocumentationFile when the project sets it back to false. XmlDocs used to stay true for such a library,
    // so nothing suppressed the documentation rules, and StyleCop reported SA0001. XmlDocs is the one switch,
    // so the module sets the file back, to the path the .NET SDK gives a library that leaves the property alone,
    // and warns.
    [Test]
    public async Task Evaluate_LibraryWithGenerateDocumentationFileOff_GeneratesDocumentationFileAndWarns()
    {
        using var home = new TempHome();
        using var plainHome = new TempHome();
        var result = Evaluate(home, "Library", "<GenerateDocumentationFile>false</GenerateDocumentationFile>");
        var plain = Evaluate(plainHome, "Library", string.Empty);
        await Assert.That(result.XmlDocs).IsEqualTo("true");
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).IsEqualTo(plain.DocumentationFile);
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
        await Assert.That(result.Warnings).IsEquivalentTo(["BVSDK1800"]);
    }

    [Test]
    public async Task Evaluate_ExeWithXmlDocsOn_GeneratesDocumentationFile()
    {
        using var home = new TempHome();
        var result = Evaluate(home, "Exe", "<XmlDocs>true</XmlDocs>");
        await Assert.That(result.XmlDocs).IsEqualTo("true");
        await Assert.That(result.GenerateDocumentationFile).IsEqualTo("true");
        await Assert.That(result.DocumentationFile).EndsWith("Test.xml");
        await Assert.That(result.NoWarn).DoesNotContain("SA0001");
        await Assert.That(result.Warnings).IsEmpty();
    }

    private static Evaluation Evaluate(TempHome home, string outputType, string properties)
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
        var warnings = project.GetItems("EvaluationWarning").Select(static item => item.EvaluatedInclude).ToList();
        return new Evaluation(
            project.GetPropertyValue("XmlDocs"),
            project.GetPropertyValue("GenerateDocumentationFile"),
            project.GetPropertyValue("DocumentationFile"),
            project.GetPropertyValue("NoWarn"),
            warnings);
    }

    private static string GetRealPath(string key)
        => typeof(XmlDocumentationModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == key)
            .Value!;
}
