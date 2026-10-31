// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.ProjectEvaluation;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Reads the model of the solution's projects that every hook's args carry, as MSBuild evaluates the projects
/// in a build configuration.
/// </summary>
/// <remarks>
/// <para>The reader runs the SDK's project evaluation target on every project of the solution, through
/// <see cref="SolutionTargetRunner"/>, and reads the files the target writes.</para>
/// <para>The reader keeps the model of each configuration it has read, so a command that runs several hooks
/// evaluates the projects once.</para>
/// </remarks>
internal sealed class SolutionInfoReader(
    ISolutionContextFactory solutions,
    SolutionTargetRunner runner,
    IReporter reporter)
{
    private static readonly SolutionTarget<ProjectEvaluationDump> Target = new(
        Name: "BV_DumpProjectEvaluation",
        OutputDirectory: CommonPaths.ProjectEvaluationDump,
        OutputDirectoryProperty: "BV_ProjectEvaluationDumpDirectory",
        ContentDescription: "project evaluation",
        TypeInfo: ProjectEvaluationDumpJsonContext.Default.ProjectEvaluationDump);

    // MSBuild compares configurations case-insensitively, but the output paths keep the case they are given,
    // and a case-sensitive file system tells them apart.
    private readonly Dictionary<string, SolutionInfo> _models = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the model of the solution's projects in the given build configuration, evaluating the projects
    /// on the first call for that configuration.
    /// </summary>
    /// <param name="configuration">The build configuration to evaluate the projects in.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is the model of the
    /// solution's projects.</returns>
    /// <exception cref="BuildFailedException">The solution file could not be parsed, the evaluation failed, or a
    /// file could not be read.</exception>
    public async Task<SolutionInfo> ReadAsync(string configuration, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(configuration);
        if (_models.TryGetValue(configuration, out var model))
        {
            return model;
        }

        model = await EvaluateAsync(configuration, cancellationToken).ConfigureAwait(false);
        _models[configuration] = model;
        return model;
    }

    /// <summary>
    /// Builds the model of a solution's projects from the files the project evaluation target wrote.
    /// </summary>
    /// <param name="solutionFile">The full path of the solution file, or <see langword="null"/> when there is
    /// none.</param>
    /// <param name="configuration">The build configuration the projects were evaluated in.</param>
    /// <param name="projectPaths">The full paths of the solution's projects, in solution order.</param>
    /// <param name="dumps">The content of each file the target wrote, in any order.</param>
    /// <returns>The model, with one entry per project path.</returns>
    /// <remarks>
    /// <para>A project the Buildvana SDK does not reach writes no file. Its entry states the path of the
    /// project file, and <see langword="null"/> for every evaluated fact.</para>
    /// </remarks>
    internal static SolutionInfo Describe(
        string? solutionFile,
        string configuration,
        IReadOnlyList<string> projectPaths,
        IReadOnlyList<ProjectEvaluationDump> dumps)
    {
        var dumpsByPath = new Dictionary<string, ProjectEvaluationDump>(StringComparer.OrdinalIgnoreCase);
        foreach (var dump in dumps)
        {
            _ = dumpsByPath.TryAdd(dump.ProjectFullPath, dump);
        }

        return new() {
            SolutionFile = solutionFile,
            Configuration = configuration,
            Projects = [
                .. projectPaths.Select(path => dumpsByPath.TryGetValue(path, out var dump) ? Evaluated(path, dump) : NotEvaluated(path)),
            ],
        };
    }

    private static ProjectInfo Evaluated(string path, ProjectEvaluationDump dump)
        => new() {
            ProjectFile = path,
            IsPackable = dump.IsPackable,
            PackAsTool = dump.PackAsTool,
            PackageId = dump.PackageId,
            TargetFrameworks = [
                .. dump.TargetFrameworks.Select(static targetFramework => new TargetFrameworkInfo {
                    TargetFramework = targetFramework.TargetFramework,
                    TargetPath = targetFramework.TargetPath,
                    DocumentationFile = targetFramework.DocumentationFile,
                    OutputType = targetFramework.OutputType,
                    IsTestProject = targetFramework.IsTestProject,
                }),
            ],
        };

    private static ProjectInfo NotEvaluated(string path)
        => new() {
            ProjectFile = path,
            IsPackable = null,
            PackAsTool = null,
            PackageId = null,
            TargetFrameworks = [],
        };

    private async Task<SolutionInfo> EvaluateAsync(string configuration, CancellationToken cancellationToken)
    {
        if (solutions.TryCreate() is not { } solution)
        {
            reporter.Info("No solution file found: hooks get no projects.");
            return Describe(solutionFile: null, configuration, projectPaths: [], dumps: []);
        }

        var projectPaths = solution.ProjectPaths;
        if (projectPaths.Count == 0)
        {
            return Describe(solution.SolutionPath, configuration, projectPaths, dumps: []);
        }

        reporter.Info($"Evaluating the solution's projects in configuration {configuration}...");
        var properties = new Dictionary<string, string> { ["Configuration"] = configuration };
        var dumps = await runner.RunAsync(Target, solution, properties, cancellationToken).ConfigureAwait(false);

        // A project whose SDK does not define the target is skipped rather than fatal, which is what lets a
        // solution hold projects the Buildvana SDK never sees. When no project at all answered, every entry
        // states nothing but its path, and the silence is worth a word.
        if (dumps.Count == 0)
        {
            reporter.Warning(
                "No project of the solution answered the project evaluation, so hooks get no evaluated facts. "
                + "Buildvana SDK may reach none of the projects.");
        }

        return Describe(solution.SolutionPath, configuration, projectPaths, dumps);
    }
}
