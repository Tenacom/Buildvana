// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.Dependencies;
using Buildvana.Sdk.Tasks;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;

internal sealed class WritePackagePinDumpTests
{
    private const string ProjectFullPath = @"C:\repo\src\Test\Test.csproj";

    [Test]
    public async Task Execute_WritesOneItemPerGivenItem()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var task = CreateTask(engine, directory);
            task.PackageVersions = [Item("Newtonsoft.Json", ("Version", "13.0.3"))];
            task.GlobalPackageReferences = [Item("Nerdbank.GitVersioning", ("Version", "3.6.0"))];
            task.PackageReferences = [Item("Serilog", ("Version", "4.0.0"))];
            await Assert.That(task.Execute()).IsTrue();
            var dump = await ReadDumpAsync(directory).ConfigureAwait(false);
            var types = string.Join(",", dump.Items.Select(static i => i.ItemType + ":" + i.Id + ":" + i.Version));
            await Assert.That(types).IsEqualTo(
                "PackageVersion:Newtonsoft.Json:13.0.3,"
                + "GlobalPackageReference:Nerdbank.GitVersioning:3.6.0,"
                + "PackageReference:Serilog:4.0.0");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_ReadsTheMetadataThatDecidesAPin()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var task = CreateTask(engine, directory);
            task.PackageVersions =
            [
                Item(
                    "Serilog",
                    ("Version", "4.0.0"),
                    ("VersionOverride", "4.1.0"),
                    ("UpdatePolicy", "patch-"),
                    ("IsImplicitlyDefined", "True"),
                    ("BV_DefiningProjectFullPath", @"C:\repo\Directory.Packages.props")),
            ];
            await Assert.That(task.Execute()).IsTrue();
            var item = (await ReadDumpAsync(directory).ConfigureAwait(false)).Items.Single();
            await Assert.That(item.VersionOverride).IsEqualTo("4.1.0");
            await Assert.That(item.UpdatePolicy).IsEqualTo("patch-");
            await Assert.That(item.IsImplicitlyDefined).IsTrue();
            await Assert.That(item.DefiningProjectFullPath).IsEqualTo(@"C:\repo\Directory.Packages.props");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithMetadataUnstated_WritesNothingForIt()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var task = CreateTask(engine, directory);
            task.PackageVersions = [Item("Serilog", ("Version", "4.0.0"))];
            await Assert.That(task.Execute()).IsTrue();
            var item = (await ReadDumpAsync(directory).ConfigureAwait(false)).Items.Single();
            await Assert.That(item.VersionOverride).IsNull();
            await Assert.That(item.UpdatePolicy).IsNull();
            await Assert.That(item.IsImplicitlyDefined).IsFalse();
            var content = await File.ReadAllTextAsync(Directory.GetFiles(directory).Single()).ConfigureAwait(false);
            await Assert.That(content).DoesNotContain("versionOverride");
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WritesTheEvaluationItDescribes()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var task = CreateTask(engine, directory);
            task.TargetFramework = "net10.0";
            task.ManagePackageVersionsCentrally = true;
            await Assert.That(task.Execute()).IsTrue();
            var dump = await ReadDumpAsync(directory).ConfigureAwait(false);
            await Assert.That(dump.ProjectFullPath).IsEqualTo(ProjectFullPath);
            await Assert.That(dump.TargetFramework).IsEqualTo("net10.0");
            await Assert.That(dump.ManagePackageVersionsCentrally).IsTrue();
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_WithNoTargetFramework_WritesNoneInTheDump()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            await Assert.That(CreateTask(engine, directory).Execute()).IsTrue();
            var dump = await ReadDumpAsync(directory).ConfigureAwait(false);
            await Assert.That(dump.TargetFramework).IsNull();
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_ForTwoTargetFrameworks_WritesTwoFiles()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
            var first = CreateTask(engine, directory);
            first.TargetFramework = "net9.0";
            var second = CreateTask(engine, directory);
            second.TargetFramework = "net10.0";
            await Assert.That(first.Execute()).IsTrue();
            await Assert.That(second.Execute()).IsTrue();
            await Assert.That(Directory.GetFiles(directory).Length).IsEqualTo(2);
        }).ConfigureAwait(false);
    }

    [Test]
    public async Task Execute_ForTwoProjectsOfOneName_WritesTwoFiles()
    {
        await RunInTempDirectory(async (engine, directory) =>
        {
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
        await Assert.That(engine.Errors[0].Message!).Contains("BVSDK1050");
    }

    private static WritePackagePinDump CreateTask(IBuildEngine engine, string outputDirectory)
        => new()
        {
            BuildEngine = engine,
            OutputDirectory = outputDirectory,
            ProjectFullPath = ProjectFullPath,
        };

    private static TaskItem Item(string id, params (string Name, string Value)[] metadata)
        => new(id, metadata.ToDictionary(static m => m.Name, static m => m.Value));

    private static async Task<PackagePinDump> ReadDumpAsync(string directory)
    {
        var content = await File.ReadAllTextAsync(Directory.GetFiles(directory).Single()).ConfigureAwait(false);
        return JsonSerializer.Deserialize(content, PackagePinDumpJsonContext.Default.PackagePinDump)!;
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
