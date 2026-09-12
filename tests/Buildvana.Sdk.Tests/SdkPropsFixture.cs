// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Sdk.props, BeforeNETSdk.targets, and Sdk.targets against a temporary repository fixture.
// The three files are copied verbatim into a stub SDK layout where the build-time generated files
// (SdkVersion.props, Import.*.proj, PackageVersions.props) and ImportConfiguration.props (which would import
// machine- and user-level configuration, breaking hermeticity) are replaced by empty stubs, and the modules
// directory is empty. Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class SdkPropsFixture : IDisposable
{
    private static readonly string[] StubFileNames =
    [
        "SdkVersion.props",
        "ImportConfiguration.props",
        "PackageVersions.props",
        "Import.BeforeCommon.props.proj",
        "Import.Common.props.proj",
        "Import.AfterCommon.props.proj",
        "Import.BeforeCommon.targets.proj",
        "Import.Common.targets.proj",
        "Import.AfterCommon.targets.proj",
    ];

    private static readonly string[] ProjectTypeProperties =
    [
        "BV_IsFileBasedAppProject",
        "BV_IsNoTargetsProject",
        "BV_IsTestProject",
        "BV_IsLibraryProject",
        "BV_IsExeProject",
    ];

    private readonly string _root;
    private readonly string _sdkPropsPath;
    private readonly string _sdkTargetsPath;

    public SdkPropsFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        RepoDirectory = Path.Combine(_root, "repo");
        _ = Directory.CreateDirectory(RepoDirectory);
        (_sdkPropsPath, _sdkTargetsPath) = BuildSdkLayout(Path.Combine(_root, "sdk"));
    }

    public string RepoDirectory { get; }

    public void WriteFile(string relativePath, string content = "")
    {
        var path = Path.Combine(RepoDirectory, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    // A project with no SDK that imports Sdk.props alone, for the props file on its own.
    public SdkEvaluationResult Evaluate(string relativeProjectDirectory = "src/Test")
    {
        var projectDirectory = Path.Combine(RepoDirectory, relativeProjectDirectory);
        _ = Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "Test.proj");
        File.WriteAllText(projectPath, $"""<Project><Import Project="{_sdkPropsPath}" /></Project>""");
        return EvaluateProject(projectPath);
    }

    // A Microsoft.NET.Sdk project under the scaffold of a repository, Directory.Build.props importing Sdk.props
    // and Directory.Build.targets importing Sdk.targets, with the given properties in its body.
    public SdkEvaluationResult EvaluateNetSdkProject(string properties)
    {
        WriteFile("Directory.Build.props", $"""<Project><Import Project="{_sdkPropsPath}" /></Project>""");
        WriteFile("Directory.Build.targets", $"""<Project><Import Project="{_sdkTargetsPath}" /></Project>""");
        var projectText = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {properties}
              </PropertyGroup>
            </Project>
            """;
        WriteFile("src/Test/Test.csproj", projectText);
        return EvaluateProject(Path.Combine(RepoDirectory, "src", "Test", "Test.csproj"));
    }

    // A project with no SDK that imports Sdk.props and Sdk.targets itself, with the given properties between them.
    // Nothing imports the files that BeforeMicrosoftNETSdkTargets names, as with a project SDK that does not layer
    // on Microsoft.NET.Sdk.
    public SdkEvaluationResult EvaluateProjectWithoutSdk(string properties)
    {
        var projectText = $"""
            <Project>
              <Import Project="{_sdkPropsPath}" />
              <PropertyGroup>
                {properties}
              </PropertyGroup>
              <Import Project="{_sdkTargetsPath}" />
            </Project>
            """;
        WriteFile("src/Test/Test.proj", projectText);
        return EvaluateProject(Path.Combine(RepoDirectory, "src", "Test", "Test.proj"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static (string SdkPropsPath, string SdkTargetsPath) BuildSdkLayout(string layoutRoot)
    {
        var sdkDirectory = Path.Combine(layoutRoot, "Sdk");
        _ = Directory.CreateDirectory(sdkDirectory);
        _ = Directory.CreateDirectory(Path.Combine(layoutRoot, "Modules"));
        var sdkPropsPath = Path.Combine(sdkDirectory, "Sdk.props");
        var sdkTargetsPath = Path.Combine(sdkDirectory, "Sdk.targets");
        File.Copy(GetRealPath("RealSdkPropsPath"), sdkPropsPath);
        File.Copy(GetRealPath("RealSdkTargetsPath"), sdkTargetsPath);
        File.Copy(GetRealPath("RealBeforeNETSdkTargetsPath"), Path.Combine(sdkDirectory, "BeforeNETSdk.targets"));
        foreach (var stubFileName in StubFileNames)
        {
            File.WriteAllText(Path.Combine(sdkDirectory, stubFileName), "<Project />");
        }

        return (sdkPropsPath, sdkTargetsPath);
    }

    private static string GetRealPath(string key)
        => typeof(SdkPropsFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == key)
            .Value!;

    private static SdkEvaluationResult EvaluateProject(string projectPath)
    {
        using var collection = new ProjectCollection();
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        var errors = project.GetItems("EvaluationError")
            .Select(static item => new SdkEvaluationError(item.EvaluatedInclude, item.GetMetadataValue("Text")))
            .ToList();
        var projectType = ProjectTypeProperties.ToDictionary(static name => name, project.GetPropertyValue);
        return new SdkEvaluationResult(project.GetPropertyValue("HomeDirectory"), errors, projectType);
    }
}
