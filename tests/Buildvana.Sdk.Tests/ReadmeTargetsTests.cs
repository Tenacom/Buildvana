// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Readme.targets of the NuGetPack module against a temporary home directory.
// Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class ReadmeTargetsTests
{
    private const string ReadmeFileName = "Package-Notes.md";

    // PackageReadmeFile names the README by file name, and the module looks the name up in the project
    // directory and above it. The lookup used to read PackageLicenseFile, so a README stated by name was
    // never found.
    [Test]
    public async Task Evaluate_PackageReadmeFileNamesFileAboveProject_SetsPackageReadmePath()
    {
        using var home = new TempHome();
        home.WriteFile(ReadmeFileName, string.Empty);
        var (readmePath, errors) = Evaluate(home, ReadmeFileName);
        await Assert.That(readmePath).IsEqualTo(home.GetFullPath(ReadmeFileName));
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task Evaluate_PackageReadmeFileNamesMissingFile_ReportsBVSDK1510()
    {
        using var home = new TempHome();
        var (readmePath, errors) = Evaluate(home, ReadmeFileName);
        await Assert.That(readmePath).IsEmpty();
        await Assert.That(errors).IsEquivalentTo(["BVSDK1510"]);
    }

    private static (string ReadmePath, IReadOnlyList<string> ErrorCodes) Evaluate(TempHome home, string packageReadmeFile)
    {
        var projectPath = home.GetFullPath("src/Test/Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <PackageReadmeFile>{packageReadmeFile}</PackageReadmeFile>
              </PropertyGroup>
              <Import Project="{GetRealReadmeTargetsPath()}" />
            </Project>
            """;
        home.WriteFile("src/Test/Test.proj", projectText);

        using var collection = new ProjectCollection();
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        var errorCodes = project.GetItems("EvaluationError").Select(static item => item.EvaluatedInclude).ToList();
        return (project.GetPropertyValue("PackageReadmePath"), errorCodes);
    }

    private static string GetRealReadmeTargetsPath()
        => typeof(ReadmeTargetsTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealNuGetPackReadmeTargetsPath")
            .Value!;
}
