// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Testing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real targets of the Versioning module, together with the real pack targets of NuGet, against a
// temporary Git repository. The project declares the ComputeVersion task itself, loaded in process, as
// PinDumpFixture does. One MSBuild build manager serves the whole process, so target tests run one at a time.
[NotInParallel]
internal sealed class VersioningModuleTests
{
    // When a project that references this one is packed, NuGet runs _GetProjectVersion on this project and
    // lists the package dependency at the version it returns. The target depends on GetPackageVersionDependsOn
    // alone, so GetBuildVersion did not run before it, and the dependency was listed at 1.0.0.
    [Test]
    public async Task GetProjectVersion_ReturnsTheComputedVersion()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("VERSION", "1.2\n");
        repo.CommitAll();
        var (succeeded, errors, projectVersion) = RunGetProjectVersion(repo);
        await Assert.That(errors).IsEmpty();
        await Assert.That(succeeded).IsTrue();
        await Assert.That(projectVersion).IsEqualTo("1.2.1");
    }

    private static (bool Succeeded, IReadOnlyList<string> Errors, string? ProjectVersion) RunGetProjectVersion(
        TempGitRepo repo)
    {
        var tasksAssembly = Path.Combine(AppContext.BaseDirectory, "Buildvana.Sdk.Tasks.dll");
        var projectText = $"""
            <Project>
              <PropertyGroup>
                <HomeDirectory>{repo.RootPath}{Path.DirectorySeparatorChar}</HomeDirectory>
              </PropertyGroup>
              <UsingTask TaskName="ComputeVersion" AssemblyFile="{tasksAssembly}" />
              <Import Project="{GetRealTargetsPath()}" />
              <Import Project="$(MSBuildToolsPath)/NuGet.Build.Tasks.Pack.targets" />
            </Project>
            """;
        repo.WriteFile("Test.proj", projectText);

        var projectPath = Path.Combine(repo.RootPath, "Test.proj");
        var globalProperties = new Dictionary<string, string?>();
        using var collection = new ProjectCollection(globalProperties);
        var logger = new RecordingMSBuildLogger();
        var parameters = new BuildParameters(collection) { Loggers = [logger] };
        var request = new BuildRequestData(projectPath, globalProperties, null, ["_GetProjectVersion"], null);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        var succeeded = result.OverallResult == BuildResultCode.Success;
        var projectVersion = succeeded
            ? result.ResultsByTarget["_GetProjectVersion"].Items.SingleOrDefault()?.GetMetadata("ProjectVersion")
            : null;
        return (succeeded, logger.Errors, projectVersion);
    }

    private static string GetRealTargetsPath()
        => typeof(VersioningModuleTests).Assembly.GetMetadata("RealVersioningModuleCoreTargetsPath");
}
