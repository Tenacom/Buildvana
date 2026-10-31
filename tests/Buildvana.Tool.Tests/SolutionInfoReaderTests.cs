// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Process;
using Buildvana.Core.ProjectEvaluation;
using Buildvana.Core.Testing;
using Buildvana.Tool.Services.Solution;

// The reader spawns MSBuild, so the process runner is a fake here, scripted to write what the SDK's project
// evaluation target would have written into the directory the command line names.
internal sealed class SolutionInfoReaderTests
{
    private const string DumpDirectoryArgumentPrefix = "-property:BV_ProjectEvaluationDumpDirectory=";

    private const string Solution = """
                                    <Solution>
                                      <Project Path="src/A/A.csproj" />
                                      <Project Path="src/B/B.csproj" />
                                    </Solution>
                                    """;

    [Test]
    public async Task ReadAsync_DescribesEveryProjectOfTheSolutionFile()
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", Solution);
        var runner = new FakeProcessRunner { OnRun = WriteDumps(ProjectPath(home, "A")) };

        var solution = await CreateReader(home, runner).ReadAsync("Release").ConfigureAwait(false);

        await Assert.That(solution.SolutionFile).IsEqualTo(Path.Combine(home.RootPath, "Test.slnx"));
        await Assert.That(solution.Configuration).IsEqualTo("Release");
        await Assert.That(solution.Projects.Count).IsEqualTo(2);
        await Assert.That(solution.Projects[0].ProjectFile).IsEqualTo(ProjectPath(home, "A"));
        await Assert.That(solution.Projects[0].IsPackable).IsTrue();
        await Assert.That(solution.Projects[1].ProjectFile).IsEqualTo(ProjectPath(home, "B"));
        await Assert.That(solution.Projects[1].IsPackable).IsNull();
    }

    [Test]
    public async Task ReadAsync_EvaluatesInTheGivenConfiguration()
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", Solution);
        var runner = new FakeProcessRunner();

        _ = await CreateReader(home, runner).ReadAsync("Custom").ConfigureAwait(false);

        var args = runner.Runs.Single().Args;
        await Assert.That(args).Contains("-property:Configuration=Custom");
        await Assert.That(args.Any(static arg => arg.StartsWith(DumpDirectoryArgumentPrefix, StringComparison.Ordinal))).IsTrue();
    }

    // A project may derive its output paths from the properties a solution build gives it, so the evaluation
    // gives the same ones.
    [Test]
    public async Task ReadAsync_EvaluatesWithThePropertiesOfASolutionBuild()
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", Solution);
        var runner = new FakeProcessRunner();

        _ = await CreateReader(home, runner).ReadAsync("Release").ConfigureAwait(false);

        var args = runner.Runs.Single().Args;
        var solutionDirectory = home.RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        await Assert.That(args).Contains("-property:SolutionDir=" + solutionDirectory);
        await Assert.That(args).Contains("-property:SolutionExt=.slnx");
        await Assert.That(args).Contains("-property:SolutionFileName=Test.slnx");
        await Assert.That(args).Contains("-property:SolutionName=Test");
        await Assert.That(args).Contains("-property:SolutionPath=" + Path.Combine(home.RootPath, "Test.slnx"));
    }

    // Every hook of a command gets the model of one evaluation.
    [Test]
    public async Task ReadAsync_EvaluatesOncePerConfiguration()
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", Solution);
        var runner = new FakeProcessRunner();
        var reader = CreateReader(home, runner);

        var first = await reader.ReadAsync("Release").ConfigureAwait(false);
        var second = await reader.ReadAsync("Release").ConfigureAwait(false);
        _ = await reader.ReadAsync("Debug").ConfigureAwait(false);

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(runner.Runs.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReadAsync_WithNoSolutionFile_StatesNoProjectsAndRunsNothing()
    {
        using var home = new TempHome();
        var runner = new FakeProcessRunner();

        var solution = await CreateReader(home, runner).ReadAsync("Release").ConfigureAwait(false);

        await Assert.That(solution.SolutionFile).IsNull();
        await Assert.That(solution.Configuration).IsEqualTo("Release");
        await Assert.That(solution.Projects).IsEmpty();
        await Assert.That(runner.Runs).IsEmpty();
    }

    // A project the Buildvana SDK does not reach is skipped, so a solution of nothing but such projects
    // describes paths alone. The silence is worth a word.
    [Test]
    public async Task ReadAsync_WhenNoProjectAnswers_SaysSo()
    {
        using var home = new TempHome();
        home.WriteFile("Test.slnx", Solution);
        var reporter = new CaptureReporter();
        var runner = new SolutionTargetRunner(home.Provider, new FakeProcessRunner(), reporter);
        var reader = new SolutionInfoReader(new HomeDirectorySolutionContextFactory(home.Provider), runner, reporter);

        var solution = await reader.ReadAsync("Release").ConfigureAwait(false);

        await Assert.That(solution.Projects.Count).IsEqualTo(2);
        await Assert.That(reporter.Messages.Any(static message => message.Level == MessageLevel.Warning)).IsTrue();
    }

    [Test]
    public async Task Describe_StatesTheFactsOfAnEvaluatedProject()
    {
        var path = Path.Combine(Path.GetTempPath(), "src", "A", "A.csproj");
        var dump = new ProjectEvaluationDump {
            ProjectFullPath = path,
            IsPackable = true,
            PackAsTool = false,
            PackageId = "Contoso.A",
            TargetFrameworks = [
                new() {
                    TargetFramework = "net10.0",
                    TargetPath = "A.dll",
                    DocumentationFile = "A.xml",
                    OutputType = "Exe",
                    IsTestProject = true,
                },
            ],
        };

        var project = SolutionInfoReader.Describe("Test.slnx", "Release", [path], [dump]).Projects.Single();

        await Assert.That(project.ProjectFile).IsEqualTo(path);
        await Assert.That(project.IsPackable).IsTrue();
        await Assert.That(project.PackAsTool).IsFalse();
        await Assert.That(project.PackageId).IsEqualTo("Contoso.A");
        var targetFramework = project.TargetFrameworks.Single();
        await Assert.That(targetFramework.TargetFramework).IsEqualTo("net10.0");
        await Assert.That(targetFramework.TargetPath).IsEqualTo("A.dll");
        await Assert.That(targetFramework.DocumentationFile).IsEqualTo("A.xml");
        await Assert.That(targetFramework.OutputType).IsEqualTo("Exe");
        await Assert.That(targetFramework.IsTestProject).IsTrue();
    }

    [Test]
    public async Task Describe_StatesNothingButThePathOfAProjectThatWroteNoFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "src", "A", "A.csproj");

        var project = SolutionInfoReader.Describe("Test.slnx", "Release", [path], []).Projects.Single();

        await Assert.That(project.ProjectFile).IsEqualTo(path);
        await Assert.That(project.IsPackable).IsNull();
        await Assert.That(project.PackAsTool).IsNull();
        await Assert.That(project.PackageId).IsNull();
        await Assert.That(project.TargetFrameworks).IsEmpty();
    }

    // MSBuild states the path of a project as the driver project names it, and a solution file may name it
    // in a case of its own. Windows file systems do not tell the two apart.
    [Test]
    public async Task Describe_MatchesADumpToItsProjectWhateverTheCase()
    {
        var path = Path.Combine(Path.GetTempPath(), "src", "A", "A.csproj");
        var dump = new ProjectEvaluationDump { ProjectFullPath = path.ToUpperInvariant(), IsPackable = true };

        var project = SolutionInfoReader.Describe("Test.slnx", "Release", [path], [dump]).Projects.Single();

        await Assert.That(project.ProjectFile).IsEqualTo(path);
        await Assert.That(project.IsPackable).IsTrue();
    }

    private static SolutionInfoReader CreateReader(TempHome home, IProcessRunner runner)
        => new(
            new HomeDirectorySolutionContextFactory(home.Provider),
            new SolutionTargetRunner(home.Provider, runner, NullReporter.Instance),
            NullReporter.Instance);

    private static string ProjectPath(TempHome home, string name) => Path.Combine(home.RootPath, "src", name, name + ".csproj");

    private static Func<string, IReadOnlyList<string>, ProcessResult> WriteDumps(params string[] projectPaths)
        => (executable, args) => {
            var argument = args.Single(static arg => arg.StartsWith(DumpDirectoryArgumentPrefix, StringComparison.Ordinal));
            var directory = argument[DumpDirectoryArgumentPrefix.Length..];
            _ = Directory.CreateDirectory(directory);
            foreach (var path in projectPaths)
            {
                var dump = new ProjectEvaluationDump { ProjectFullPath = path, IsPackable = true };
                var json = JsonSerializer.Serialize(dump, ProjectEvaluationDumpJsonContext.Default.ProjectEvaluationDump);
                File.WriteAllText(Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + ".json"), json);
            }

            return new ProcessResult(executable, 0, string.Empty, string.Empty, TimeSpan.Zero);
        };
}
