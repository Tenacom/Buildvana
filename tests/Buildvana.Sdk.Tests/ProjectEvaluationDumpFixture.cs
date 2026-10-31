// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;
using Buildvana.Core.ProjectEvaluation;
using Buildvana.Core.Testing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

// Runs the real project evaluation target against a temporary project, and reads back what it wrote.
// The project imports the module file under test and declares the task itself, as in PinDumpFixture. The
// project states the properties and items the .NET SDK would evaluate, such as TargetPath and FinalDocFile,
// so the test exercises which evaluation each fact comes from, and not the .NET SDK's own logic.
internal sealed class ProjectEvaluationDumpFixture : IDisposable
{
    private readonly string _root;

    public ProjectEvaluationDumpFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "bvtest_" + Guid.NewGuid().ToString("N"));
        ProjectDirectory = Path.Combine(_root, "project");
        DumpDirectory = Path.Combine(_root, "dump");
        _ = Directory.CreateDirectory(ProjectDirectory);
        _ = Directory.CreateDirectory(DumpDirectory);
    }

    public string ProjectDirectory { get; }

    public string DumpDirectory { get; }

    public string ProjectPath => Path.Combine(ProjectDirectory, "Test.proj");

    // Writes a project made of the given body, runs the project evaluation target on it, and returns the
    // one dump it wrote.
    public ProjectEvaluationDump Dump(string projectBody)
    {
        var tasksAssembly = Path.Combine(AppContext.BaseDirectory, "Buildvana.Sdk.Tasks.dll");
        var content = $"""
                       <Project>
                         <UsingTask TaskName="WriteProjectEvaluationDump" AssemblyFile="{tasksAssembly}" />
                       {projectBody}
                         <Import Project="{GetMetadata("RealProjectEvaluationModuleTargetsPath")}" />
                       </Project>
                       """;
        WriteFile(ProjectPath, content);
        Run(ProjectPath);
        return ReadDump(Directory.EnumerateFiles(DumpDirectory).Single());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string GetMetadata(string key)
        => typeof(ProjectEvaluationDumpFixture).Assembly.GetMetadata(key);

    private static void WriteFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        Debug.Assert(directory is not null, "GetDirectoryName returns null only for a root, and a file path is not one.");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }

    private static ProjectEvaluationDump ReadDump(string path)
        => JsonSerializer.Deserialize(File.ReadAllText(path), ProjectEvaluationDumpJsonContext.Default.ProjectEvaluationDump)
            ?? throw new InvalidOperationException($"The project evaluation dump '{path}' reads as null.");

    private void Run(string projectPath)
    {
        var globalProperties = new Dictionary<string, string?> {
            ["BV_ProjectEvaluationDumpDirectory"] = DumpDirectory + Path.DirectorySeparatorChar,
        };
        using var collection = new ProjectCollection(globalProperties);
        var logger = new RecordingMSBuildLogger();
        var parameters = new BuildParameters(collection) { Loggers = [logger] };
        var request = new BuildRequestData(projectPath, globalProperties, null, ["BV_DumpProjectEvaluation"], null);
        var result = BuildManager.DefaultBuildManager.Build(parameters, request);
        if (result.OverallResult != BuildResultCode.Success)
        {
            throw new InvalidOperationException("The project evaluation target failed: " + string.Join("; ", logger.Errors));
        }
    }
}
