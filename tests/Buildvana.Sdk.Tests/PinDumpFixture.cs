// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;
using Buildvana.Core.Dependencies;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real pin dump target against a temporary project, and reads back what it wrote.
// The project imports the module file under test and declares the task itself, so the test exercises the
// target's own logic - which items it dumps, and how it treats a multi-targeting project - without a stub
// of the whole SDK layout around it. The task is loaded in process: the task host the SDK asks for is
// MSBuild's business, not this target's.
internal sealed class PinDumpFixture : IDisposable
{
    private readonly string _root;

    public PinDumpFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        ProjectDirectory = Path.Combine(_root, "project");
        DumpDirectory = Path.Combine(_root, "dump");
        _ = Directory.CreateDirectory(ProjectDirectory);
        _ = Directory.CreateDirectory(DumpDirectory);
    }

    public string ProjectDirectory { get; }

    public string DumpDirectory { get; }

    public string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(ProjectDirectory, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    // Writes a project made of the given body, runs the pin dump target on it, and returns the dumps it
    // wrote, ordered by target framework so that a multi-targeting project reads back predictably.
    public IReadOnlyList<PackagePinDump> DumpPins(string projectBody)
    {
        var tasksAssembly = Path.Combine(AppContext.BaseDirectory, "Buildvana.Sdk.Tasks.dll");
        var content = $"""
                       <Project>
                         <UsingTask TaskName="WritePackagePinDump" AssemblyFile="{tasksAssembly}" />
                       {projectBody}
                         <Import Project="{GetMetadata("RealDependenciesModulePath")}" />
                       </Project>
                       """;
        Run(WriteFile("Test.proj", content));
        return [.. Directory.EnumerateFiles(DumpDirectory)
            .Select(static path => JsonSerializer.Deserialize(File.ReadAllText(path), PackagePinDumpJsonContext.Default.PackagePinDump)!)
            .OrderBy(static dump => dump.TargetFramework, StringComparer.Ordinal)];
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string GetMetadata(string key)
        => typeof(PinDumpFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == key)
            .Value!;

    private void Run(string projectPath)
    {
        var globalProperties = new Dictionary<string, string?> { ["BV_PinDumpDirectory"] = DumpDirectory + Path.DirectorySeparatorChar };
        using var collection = new ProjectCollection(globalProperties);
        var logger = new RecordingMSBuildLogger();
        var parameters = new BuildParameters(collection) { Loggers = [logger] };
        var request = new BuildRequestData(projectPath, globalProperties, null, ["BV_DumpPackagePins"], null);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        if (result.OverallResult != BuildResultCode.Success)
        {
            throw new InvalidOperationException("The pin dump target failed: " + string.Join("; ", logger.Errors));
        }
    }
}
