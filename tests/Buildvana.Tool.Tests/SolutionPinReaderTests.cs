// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.Process;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Dependencies;

// The reader spawns MSBuild, so the process runner is a fake here, scripted to write what the SDK's dump
// target would have written into the directory the command line names.
internal sealed class SolutionPinReaderTests
{
    private const string ProjectPath = @"C:\repo\src\A\A.csproj";
    private const string DumpDirectoryArgumentPrefix = "-property:BV_PinDumpDirectory=";

    [Test]
    public async Task ReadAsync_RunsTheDumpTargetThroughADriverProject()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner { OnRun = WriteDumps("A", "net10.0") };
        _ = await CreateReader(home, runner).ReadAsync([ProjectPath]).ConfigureAwait(false);
        var args = string.Join(" ", runner.Runs.Single().Args);
        await Assert.That(args).Contains("msbuild");
        await Assert.That(args).Contains("pin-dump.proj");
        await Assert.That(args).Contains("-target:" + PinDumpDriverProject.TargetName);
        await Assert.That(args).Contains(DumpDirectoryArgumentPrefix);
        await Assert.That(args).Contains("-property:BV_SuppressTransitiveOverrides=true");
    }

    [Test]
    public async Task ReadAsync_ReturnsOneDumpPerFileTheTargetWrote()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner { OnRun = WriteDumps("A", "net9.0", "net10.0") };
        var dumps = await CreateReader(home, runner).ReadAsync([ProjectPath]).ConfigureAwait(false);
        await Assert.That(dumps.Count).IsEqualTo(2);
        await Assert.That(dumps.Select(static dump => dump.TargetFramework)).Contains("net10.0");
    }

    // The directory holds the files of the last run, and a project dropped from the solution must not go on
    // being read from it.
    [Test]
    public async Task ReadAsync_ForgetsWhatAnEarlierRunWrote()
    {
        using var home = new TempHome();
        WriteStaleDump(home);
        var runner = new FakeProcessRunner { OnRun = WriteDumps("A", "net10.0") };
        var dumps = await CreateReader(home, runner).ReadAsync([ProjectPath]).ConfigureAwait(false);
        await Assert.That(dumps.Single().ProjectFullPath).Contains("A.csproj");
    }

    [Test]
    public async Task ReadAsync_WithNoProject_RunsNothing()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner();
        var dumps = await CreateReader(home, runner).ReadAsync([]).ConfigureAwait(false);
        await Assert.That(dumps).IsEmpty();
        await Assert.That(runner.Runs).IsEmpty();
    }

    [Test]
    public async Task ReadAsync_WhenEvaluationFails_ReportsAFailedStep()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner
        {
            OnRun = static (executable, _)
                => new ProcessResult(executable, 1, "A.csproj(1,2): error MSB4025: no.", string.Empty, TimeSpan.Zero),
        };

        var reader = CreateReader(home, runner);
        var exception = await Assert.That(async () => await reader.ReadAsync([ProjectPath]).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(3);
    }

    [Test]
    public async Task ReadAsync_WithAnUnreadableDump_ReportsAFailedStep()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner { OnRun = WriteText("broken.json", "{ not json") };
        var reader = CreateReader(home, runner);
        var exception = await Assert.That(async () => await reader.ReadAsync([ProjectPath]).ConfigureAwait(false))
            .Throws<BuildFailedException>();
        await Assert.That(exception!.ExitCode).IsEqualTo(3);
    }

    private static SolutionPinReader CreateReader(TempHome home, IProcessRunner runner)
        => new(home.Provider, runner, NullReporter.Instance);

    private static Func<string, IReadOnlyList<string>, ProcessResult> WriteDumps(string projectName, params string[] targetFrameworks)
        => (executable, args) =>
        {
            var directory = DumpDirectoryOf(args);
            foreach (var targetFramework in targetFrameworks)
            {
                var dump = new PackagePinDump
                {
                    ProjectFullPath = $@"C:\repo\src\{projectName}\{projectName}.csproj",
                    TargetFramework = targetFramework,
                };

                Write(directory, $"{projectName}-{targetFramework}.json", Serialize(dump));
            }

            return new ProcessResult(executable, 0, string.Empty, string.Empty, TimeSpan.Zero);
        };

    private static Func<string, IReadOnlyList<string>, ProcessResult> WriteText(string fileName, string content)
        => (executable, args) =>
        {
            Write(DumpDirectoryOf(args), fileName, content);
            return new ProcessResult(executable, 0, string.Empty, string.Empty, TimeSpan.Zero);
        };

    private static void WriteStaleDump(TempHome home)
    {
        var dump = new PackagePinDump { ProjectFullPath = @"C:\repo\src\Gone\Gone.csproj" };
        Write(home.GetFullPath(".buildvana-temp/pin-dump"), "gone.json", Serialize(dump));
    }

    private static string Serialize(PackagePinDump dump)
        => JsonSerializer.Serialize(dump, PackagePinDumpJsonContext.Default.PackagePinDump);

    private static void Write(string directory, string fileName, string content)
    {
        _ = Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }

    private static string DumpDirectoryOf(IReadOnlyList<string> args)
        => args.Single(static arg => arg.StartsWith(DumpDirectoryArgumentPrefix, StringComparison.Ordinal))[DumpDirectoryArgumentPrefix.Length..];
}
