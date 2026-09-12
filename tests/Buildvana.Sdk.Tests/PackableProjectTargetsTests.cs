// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Buildvana.Core.Testing;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

// Evaluates the real Module.Core.PackableProject.targets of the NuGetPack module against a temporary home
// directory. Evaluation-only: no targets are executed, so assertions are limited to properties and items.
// The README, third-party notice, and icon lookups are off, so that the license alone drives the result.
internal sealed class PackableProjectTargetsTests
{
    // PackageRequireLicenseAcceptance is forced to false when the package has neither a license expression
    // nor a license file. The condition used to test LicensePackageExpression, a property nothing defines,
    // so a license expression, which turns the license file off, forced the property to false.
    [Test]
    public async Task Evaluate_LicenseExpression_KeepsPackageRequireLicenseAcceptance()
    {
        using var home = new TempHome();
        var (requireLicenseAcceptance, errors) = Evaluate(home, "<PackageLicenseExpression>MIT</PackageLicenseExpression>");
        await Assert.That(requireLicenseAcceptance).IsEqualTo("true");
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task Evaluate_LicenseFileFound_KeepsPackageRequireLicenseAcceptance()
    {
        using var home = new TempHome();
        home.WriteFile("LICENSE", string.Empty);
        var (requireLicenseAcceptance, errors) = Evaluate(home, string.Empty);
        await Assert.That(requireLicenseAcceptance).IsEqualTo("true");
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task Evaluate_NoLicense_ForcesPackageRequireLicenseAcceptanceOff()
    {
        using var home = new TempHome();
        var (requireLicenseAcceptance, errors) = Evaluate(home, "<LicenseFileInPackage>false</LicenseFileInPackage>");
        await Assert.That(requireLicenseAcceptance).IsEqualTo("false");
        await Assert.That(errors).IsEmpty();
    }

    private static (string RequireLicenseAcceptance, IReadOnlyList<string> ErrorCodes) Evaluate(
        TempHome home,
        string licenseProperty)
    {
        var projectPath = home.GetFullPath("src/Test/Test.proj");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <HomeDirectory>{home.RootPath}{Path.DirectorySeparatorChar}</HomeDirectory>
                <ReadmeFileInPackage>false</ReadmeFileInPackage>
                <ThirdPartyNoticeInPackage>false</ThirdPartyNoticeInPackage>
                <IconInPackage>false</IconInPackage>
                <PackageRequireLicenseAcceptance>true</PackageRequireLicenseAcceptance>
                {licenseProperty}
              </PropertyGroup>
              <Import Project="{GetRealPackableProjectTargetsPath()}" />
            </Project>
            """;
        home.WriteFile("src/Test/Test.proj", projectText);

        using var collection = new ProjectCollection();
        var project = Project.FromFile(projectPath, new ProjectOptions { ProjectCollection = collection });
        var errorCodes = project.GetItems("EvaluationError").Select(static item => item.EvaluatedInclude).ToList();
        return (project.GetPropertyValue("PackageRequireLicenseAcceptance"), errorCodes);
    }

    private static string GetRealPackableProjectTargetsPath()
        => typeof(PackableProjectTargetsTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(static attribute => attribute.Key == "RealNuGetPackPackableProjectTargetsPath")
            .Value!;
}
