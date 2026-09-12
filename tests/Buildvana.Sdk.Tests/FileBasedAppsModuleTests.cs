// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Module.targets of the FileBasedApps module, imported from Directory.Build.targets, in a
// Microsoft.NET.Sdk project of a temporary home directory. The project states BV_IsFileBasedAppProject, which
// BeforeNETSdk.targets computes in a real build, and the version of Buildvana SDK. Its PackageReference item
// stands in for the one the .NET CLI writes for a `#:package` directive.
// Evaluation-only: no targets are executed, so assertions are limited to properties and items.
internal sealed class FileBasedAppsModuleTests
{
    private const string SdkVersion = "1.2.3";
    private const string CentralPackageManagement = "<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>";
    private const string UnversionedReference = """<PackageReference Include="Buildvana.Runtime" />""";

    [Test]
    public async Task Evaluate_FileBasedApp_SuppressesSA1402AndSA1649()
    {
        using var home = new TempHome();
        var result = Evaluate(home, fileBasedApp: true, string.Empty, string.Empty);
        var noWarn = result.NoWarn.Split(';');
        await Assert.That(noWarn).Contains("SA1402");
        await Assert.That(noWarn).Contains("SA1649");
    }

    [Test]
    public async Task Evaluate_OtherProject_LeavesNoWarnAlone()
    {
        using var home = new TempHome();
        var result = Evaluate(home, fileBasedApp: false, string.Empty, string.Empty);
        var noWarn = result.NoWarn.Split(';');
        await Assert.That(noWarn).DoesNotContain("SA1402");
        await Assert.That(noWarn).DoesNotContain("SA1649");
    }

    [Test]
    public async Task Evaluate_CentralPackageManagement_AddsThePackageVersion()
    {
        using var home = new TempHome();
        var result = Evaluate(home, fileBasedApp: true, CentralPackageManagement, UnversionedReference);
        await Assert.That(result.PackageVersion).IsEqualTo(SdkVersion);
        await Assert.That(result.PackageReferenceVersion).IsEmpty();
    }

    [Test]
    public async Task Evaluate_CentralPackageManagementWithPackageVersion_LeavesItAlone()
    {
        using var home = new TempHome();
        const string items = """
            <PackageReference Include="Buildvana.Runtime" />
            <PackageVersion Include="Buildvana.Runtime" Version="9.9.9" />
            """;
        var result = Evaluate(home, fileBasedApp: true, CentralPackageManagement, items);
        await Assert.That(result.PackageVersion).IsEqualTo("9.9.9");
    }

    [Test]
    public async Task Evaluate_NoCentralPackageManagement_WritesTheVersionIntoThePackageReference()
    {
        using var home = new TempHome();
        var result = Evaluate(home, fileBasedApp: true, string.Empty, UnversionedReference);
        await Assert.That(result.PackageReferenceVersion).IsEqualTo(SdkVersion);
        await Assert.That(result.PackageVersion).IsEmpty();
    }

    [Test]
    public async Task Evaluate_NoCentralPackageManagementWithVersion_LeavesItAlone()
    {
        using var home = new TempHome();
        const string items = """<PackageReference Include="Buildvana.Runtime" Version="9.9.9" />""";
        var result = Evaluate(home, fileBasedApp: true, string.Empty, items);
        await Assert.That(result.PackageReferenceVersion).IsEqualTo("9.9.9");
    }

    [Test]
    public async Task Evaluate_OtherProject_AddsNoPin()
    {
        using var home = new TempHome();
        var result = Evaluate(home, fileBasedApp: false, CentralPackageManagement, UnversionedReference);
        await Assert.That(result.PackageVersion).IsEmpty();
        await Assert.That(result.PackageReferenceVersion).IsEmpty();
    }

    private static (string NoWarn, string PackageVersion, string PackageReferenceVersion) Evaluate(
        TempHome home,
        bool fileBasedApp,
        string properties,
        string items)
    {
        var targetsText = $"""
            <Project>
              <Import Project="{GetRealTargetsPath()}" />
            </Project>
            """;
        home.WriteFile("Directory.Build.targets", targetsText);
        var fileBasedAppValue = fileBasedApp ? "true" : "false";
        var projectPath = home.GetFullPath("src/Test/Test.csproj");
        var projectText = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <BV_IsFileBasedAppProject>{fileBasedAppValue}</BV_IsFileBasedAppProject>
                <BuildvanaSdkVersion>{SdkVersion}</BuildvanaSdkVersion>
                {properties}
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
            </Project>
            """;
        home.WriteFile("src/Test/Test.csproj", projectText);

        using var collection = new ProjectCollection();
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        return (
            project.GetPropertyValue("NoWarn"),
            GetVersion(project, "PackageVersion"),
            GetVersion(project, "PackageReference"));
    }

    private static string GetVersion(Project project, string itemType)
        => project.GetItems(itemType)
            .SingleOrDefault(static item => item.EvaluatedInclude == "Buildvana.Runtime")
            ?.GetMetadataValue("Version") ?? string.Empty;

    private static string GetRealTargetsPath()
        => typeof(FileBasedAppsModuleTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealFileBasedAppsModuleTargetsPath")
            .Value!;
}
