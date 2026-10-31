// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

// One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class ProjectEvaluationDumpTests
{
    [Test]
    public async Task Dump_StatesThePackFactsOfTheProject()
    {
        using var fixture = new ProjectEvaluationDumpFixture();
        var dump = fixture.Dump(
            """
              <PropertyGroup>
                <IsPackable>true</IsPackable>
                <PackAsTool>true</PackAsTool>
                <PackageId>Example.Tool</PackageId>
              </PropertyGroup>
            """);

        await Assert.That(dump.ProjectFullPath).IsEqualTo(fixture.ProjectPath);
        await Assert.That(dump.IsPackable).IsTrue();
        await Assert.That(dump.PackAsTool).IsTrue();
        await Assert.That(dump.PackageId).IsEqualTo("Example.Tool");
    }

    [Test]
    public async Task Dump_OfAProjectWithNoTargetFramework_HasNoTargetFrameworkEntry()
    {
        using var fixture = new ProjectEvaluationDumpFixture();
        var dump = fixture.Dump(
            """
              <PropertyGroup>
                <TargetPath>$(MSBuildProjectDirectory)/bin/Test.dll</TargetPath>
              </PropertyGroup>
            """);

        await Assert.That(dump.TargetFrameworks).IsEmpty();
    }

    [Test]
    public async Task Dump_OfAProjectWithOneTargetFramework_StatesTheFactsOfTheFramework()
    {
        using var fixture = new ProjectEvaluationDumpFixture();
        var dump = fixture.Dump(
            """
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <TargetPath>$(MSBuildProjectDirectory)/bin/Test.dll</TargetPath>
                <OutputType>Exe</OutputType>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <FinalDocFile Include="bin/Test.xml" />
              </ItemGroup>
            """);

        var targetFramework = dump.TargetFrameworks.Single();
        await Assert.That(targetFramework.TargetFramework).IsEqualTo("net10.0");

        // The separators of TargetPath are the ones the property states, on both platforms: MSBuild
        // normalizes a path in an import or an item, never in a property value.
        await Assert.That(targetFramework.TargetPath).IsEqualTo(fixture.ProjectDirectory + "/bin/Test.dll");
        await Assert.That(targetFramework.DocumentationFile).IsEqualTo(Path.Combine(fixture.ProjectDirectory, "bin", "Test.xml"));
        await Assert.That(targetFramework.OutputType).IsEqualTo("Exe");
        await Assert.That(targetFramework.IsTestProject).IsTrue();
    }

    // A package sets OutputType and IsTestProject in the evaluation of a target framework alone, as TUnit does,
    // and dotnet pack reads IsPackable in the outer build. The conditions on TargetFramework stand for both.
    [Test]
    public async Task Dump_OfAMultiTargetingProject_TakesEachFactFromTheEvaluationThatStatesIt()
    {
        using var fixture = new ProjectEvaluationDumpFixture();
        var dump = fixture.Dump(
            """
              <PropertyGroup>
                <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
                <TargetPath>$(MSBuildProjectDirectory)/bin/$(TargetFramework)/Test.dll</TargetPath>
              </PropertyGroup>
              <PropertyGroup Condition="'$(TargetFramework)' == ''">
                <IsPackable>true</IsPackable>
              </PropertyGroup>
              <PropertyGroup Condition="'$(TargetFramework)' == 'net10.0'">
                <OutputType>Exe</OutputType>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
            """);

        await Assert.That(dump.IsPackable).IsTrue();
        await Assert.That(dump.TargetFrameworks.Count).IsEqualTo(2);
        var (first, second) = (dump.TargetFrameworks[0], dump.TargetFrameworks[1]);
        await Assert.That(first.TargetFramework).IsEqualTo("net9.0");
        await Assert.That(first.TargetPath).IsEqualTo(fixture.ProjectDirectory + "/bin/net9.0/Test.dll");
        await Assert.That(first.OutputType).IsNull();
        await Assert.That(first.IsTestProject).IsFalse();
        await Assert.That(second.TargetFramework).IsEqualTo("net10.0");
        await Assert.That(second.TargetPath).IsEqualTo(fixture.ProjectDirectory + "/bin/net10.0/Test.dll");
        await Assert.That(second.OutputType).IsEqualTo("Exe");
        await Assert.That(second.IsTestProject).IsTrue();
    }

    [Test]
    public async Task Dump_WithFactsUnstated_StatesNoneOfThem()
    {
        using var fixture = new ProjectEvaluationDumpFixture();
        var dump = fixture.Dump(
            """
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <TargetPath>$(MSBuildProjectDirectory)/bin/Test.dll</TargetPath>
              </PropertyGroup>
            """);

        await Assert.That(dump.IsPackable).IsFalse();
        await Assert.That(dump.PackAsTool).IsFalse();
        await Assert.That(dump.PackageId).IsNull();
        var targetFramework = dump.TargetFrameworks.Single();
        await Assert.That(targetFramework.DocumentationFile).IsNull();
        await Assert.That(targetFramework.OutputType).IsNull();
        await Assert.That(targetFramework.IsTestProject).IsFalse();
    }
}
