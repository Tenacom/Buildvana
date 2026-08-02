// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Sdk.props against a temporary repository fixture.
// The file under test is copied verbatim into a stub SDK layout where the build-time generated files
// (SdkVersion.props, Import.*.props.proj) and ImportConfiguration.props (which would import machine- and
// user-level configuration, breaking hermeticity) are replaced by empty stubs.
// Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class SdkPropsFixture : IDisposable
{
    private static readonly string[] StubFileNames =
    [
        "SdkVersion.props",
        "ImportConfiguration.props",
        "Import.BeforeCommon.props.proj",
        "Import.Common.props.proj",
        "Import.AfterCommon.props.proj",
    ];

    private readonly string _root;
    private readonly string _sdkPropsPath;

    public SdkPropsFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        RepoDirectory = Path.Combine(_root, "repo");
        _ = Directory.CreateDirectory(RepoDirectory);
        _sdkPropsPath = BuildSdkLayout(Path.Combine(_root, "sdk"));
    }

    public string RepoDirectory { get; }

    public void WriteFile(string relativePath, string content = "")
    {
        var path = Path.Combine(RepoDirectory, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public SdkEvaluationResult Evaluate(string relativeProjectDirectory = "src/Test")
    {
        var projectDirectory = Path.Combine(RepoDirectory, relativeProjectDirectory);
        _ = Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "Test.proj");
        File.WriteAllText(projectPath, $"""<Project><Import Project="{_sdkPropsPath}" /></Project>""");
        return EvaluateProject(projectPath);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string BuildSdkLayout(string layoutRoot)
    {
        var sdkDirectory = Path.Combine(layoutRoot, "Sdk");
        _ = Directory.CreateDirectory(sdkDirectory);
        var sdkPropsPath = Path.Combine(sdkDirectory, "Sdk.props");
        File.Copy(GetRealSdkPropsPath(), sdkPropsPath);
        foreach (var stubFileName in StubFileNames)
        {
            File.WriteAllText(Path.Combine(sdkDirectory, stubFileName), "<Project />");
        }

        return sdkPropsPath;
    }

    private static string GetRealSdkPropsPath()
        => typeof(SdkPropsFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealSdkPropsPath")
            .Value!;

    private static SdkEvaluationResult EvaluateProject(string projectPath)
    {
        using var collection = new ProjectCollection();
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        var errors = project.GetItems("EvaluationError")
            .Select(static item => new SdkEvaluationError(item.EvaluatedInclude, item.GetMetadataValue("Text")))
            .ToList();
        return new SdkEvaluationResult(project.GetPropertyValue("HomeDirectory"), errors);
    }
}
