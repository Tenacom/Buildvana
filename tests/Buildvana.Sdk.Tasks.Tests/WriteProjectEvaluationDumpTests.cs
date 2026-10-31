// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.ProjectEvaluation;
using Buildvana.Sdk.Tasks;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

internal sealed class WriteProjectEvaluationDumpTests
{
    private const string ProjectFullPath = @"C:\repo\src\Test\Test.csproj";

    [Test]
    public async Task Execute_WritesTheFactsOfTheProject()
    {
        await RunInTempDirectory(async (engine, directory) => {
            var task = CreateTask(engine, directory);
            task.IsPackable = true;
            task.PackAsTool = true;
            task.PackageId = "Example.Tool";
            await Assert.That(task.Execute()).IsTrue();
            var dump = await ReadDumpAsync(directory).ConfigureAwait(false);
            await Assert.That(dump.ProjectFullPath).IsEqualTo(ProjectFullPath);
            await Assert.That(dump.IsPackable).IsTrue();
            await Assert.That(dump.PackAsTool).IsTrue();
            await Assert.That(dump.PackageId).IsEqualTo("Example.Tool");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WritesOneEntryPerGivenTargetFramework()
    {
        await RunInTempDirectory(async (engine, directory) => {
            var task = CreateTask(engine, directory);
            task.TargetFrameworks = [
                Item("net9.0", ("TargetPath", @"C:\repo\bin\net9.0\Test.dll")),
                Item(
                    "net10.0",
                    ("TargetPath", @"C:\repo\bin\net10.0\Test.dll"),
                    ("DocumentationFile", @"C:\repo\bin\net10.0\Test.xml"),
                    ("OutputType", "Exe"),
                    ("IsTestProject", "True")),
            ];
            await Assert.That(task.Execute()).IsTrue();
            var targetFrameworks = (await ReadDumpAsync(directory).ConfigureAwait(false)).TargetFrameworks;
            await Assert.That(targetFrameworks.Count).IsEqualTo(2);
            await Assert.That(targetFrameworks[0].TargetFramework).IsEqualTo("net9.0");
            await Assert.That(targetFrameworks[0].TargetPath).IsEqualTo(@"C:\repo\bin\net9.0\Test.dll");
            var second = targetFrameworks[1];
            await Assert.That(second.TargetFramework).IsEqualTo("net10.0");
            await Assert.That(second.DocumentationFile).IsEqualTo(@"C:\repo\bin\net10.0\Test.xml");
            await Assert.That(second.OutputType).IsEqualTo("Exe");
            await Assert.That(second.IsTestProject).IsTrue();
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithValuesUnstated_WritesNothingForThem()
    {
        await RunInTempDirectory(async (engine, directory) => {
            var task = CreateTask(engine, directory);
            task.TargetFrameworks = [Item("net10.0", ("TargetPath", @"C:\repo\bin\Test.dll"))];
            await Assert.That(task.Execute()).IsTrue();
            var dump = await ReadDumpAsync(directory).ConfigureAwait(false);
            await Assert.That(dump.PackageId).IsNull();
            var targetFramework = dump.TargetFrameworks.Single();
            await Assert.That(targetFramework.DocumentationFile).IsNull();
            await Assert.That(targetFramework.OutputType).IsNull();
            await Assert.That(targetFramework.IsTestProject).IsFalse();
            var content = await File.ReadAllTextAsync(Directory.GetFiles(directory).Single()).ConfigureAwait(false);
            await Assert.That(content).DoesNotContain("documentationFile");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_ForTwoProjectsOfOneName_WritesTwoFiles()
    {
        await RunInTempDirectory(async (engine, directory) => {
            var first = CreateTask(engine, directory);
            var second = CreateTask(engine, directory);
            second.ProjectFullPath = @"C:\repo\other\Test.csproj";
            await Assert.That(first.Execute()).IsTrue();
            await Assert.That(second.Execute()).IsTrue();
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(2);
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithMissingOutputDirectory_LogsError()
    {
        var engine = new RecordingBuildEngine();
        var task = CreateTask(engine, string.Empty);
        await Assert.That(task.Execute()).IsFalse();
        await Assert.That(engine.Errors.Count).IsEqualTo(1);
        await Assert.That(engine.Errors[0].Message).Contains("BVSDK1050");
    }

    private static WriteProjectEvaluationDump CreateTask(IBuildEngine engine, string outputDirectory)
        => new() {
            BuildEngine = engine,
            OutputDirectory = outputDirectory,
            ProjectFullPath = ProjectFullPath,
        };

    private static TaskItem Item(string targetFramework, params (string Name, string Value)[] metadata)
        => new(targetFramework, metadata.ToDictionary(static m => m.Name, static m => m.Value));

    private static async Task<ProjectEvaluationDump> ReadDumpAsync(string directory)
    {
        var path = Directory.GetFiles(directory).Single();
        var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return JsonSerializer.Deserialize(content, ProjectEvaluationDumpJsonContext.Default.ProjectEvaluationDump)
            ?? throw new InvalidOperationException($"The project evaluation dump '{path}' reads as null.");
    }

    private static async Task RunInTempDirectory(Func<RecordingBuildEngine, string, Task> test)
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            await test(new RecordingBuildEngine(), tempDirectory.FullName).ConfigureAwait(false);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
