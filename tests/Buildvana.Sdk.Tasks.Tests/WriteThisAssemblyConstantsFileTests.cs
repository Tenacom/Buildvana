// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Sdk.Tasks;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

internal sealed class WriteThisAssemblyConstantsFileTests
{
    [Test]
    public async Task Execute_WritesEncodedConstants()
    {
        await RunInTempDirectory(async (engine, outputPath) =>
        {
            var task = CreateTask(engine, outputPath, ("Answer", "42"), ("My Constant", "string:Hello World"), ("Tricky", "a=b\nc;d"));
            await Assert.That(task.Execute()).IsTrue();
            var content = await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
            await Assert.That(content).IsEqualTo("Answer=42\nMy%20Constant=string%3AHello%20World\nTricky=a%3Db%0Ac%3Bd\n");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_TrimsNamesAndValues()
    {
        await RunInTempDirectory(async (engine, outputPath) =>
        {
            var task = CreateTask(engine, outputPath, ("  Answer  ", "  42  "));
            await Assert.That(task.Execute()).IsTrue();
            var content = await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
            await Assert.That(content).IsEqualTo("Answer=42\n");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithNoConstants_WritesEmptyFile()
    {
        await RunInTempDirectory(async (engine, outputPath) =>
        {
            var task = CreateTask(engine, outputPath);
            await Assert.That(task.Execute()).IsTrue();
            await Assert.That(File.Exists(outputPath)).IsTrue();
            var content = await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
            await Assert.That(content).IsEqualTo(string.Empty);
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithUnchangedContent_DoesNotRewriteFile()
    {
        await RunInTempDirectory(async (engine, outputPath) =>
        {
            await Assert.That(CreateTask(engine, outputPath, ("Answer", "42")).Execute()).IsTrue();
            var pastTime = DateTime.UtcNow.AddHours(-1);
            File.SetLastWriteTimeUtc(outputPath, pastTime);
            await Assert.That(CreateTask(engine, outputPath, ("Answer", "42")).Execute()).IsTrue();
            await Assert.That(File.GetLastWriteTimeUtc(outputPath)).IsEqualTo(pastTime);
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithChangedContent_RewritesFile()
    {
        await RunInTempDirectory(async (engine, outputPath) =>
        {
            await Assert.That(CreateTask(engine, outputPath, ("Answer", "42")).Execute()).IsTrue();
            await Assert.That(CreateTask(engine, outputPath, ("Answer", "13")).Execute()).IsTrue();
            var content = await File.ReadAllTextAsync(outputPath).ConfigureAwait(false);
            await Assert.That(content).IsEqualTo("Answer=13\n");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithMissingOutputPath_LogsError()
    {
        var engine = new RecordingBuildEngine();
        var task = CreateTask(engine, string.Empty, ("Answer", "42"));
        await Assert.That(task.Execute()).IsFalse();
        await Assert.That(engine.Errors.Count).IsEqualTo(1);
        await Assert.That(engine.Errors[0].Message!).Contains("BVSDK1050");
    }

    private static WriteThisAssemblyConstantsFile CreateTask(
        IBuildEngine engine,
        string outputPath,
        params (string Name, string Value)[] constants)
        => new()
        {
            BuildEngine = engine,
            OutputPath = outputPath,
            Constants = [.. constants.Select(c => new TaskItem(c.Name, new Dictionary<string, string> { ["Value"] = c.Value }))],
        };

    private static async Task RunInTempDirectory(Func<RecordingBuildEngine, string, Task> test)
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "Test.ThisAssemblyConstants.txt");
            await test(new RecordingBuildEngine(), outputPath).ConfigureAwait(false);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
