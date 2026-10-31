// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Diagnostics;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Solution;

/// <summary>
/// Runs a target of the Buildvana SDK on every project of a solution, and reads the files the target writes.
/// </summary>
/// <remarks>
/// <para>The runner writes a driver project (see <see cref="SolutionDriverProject"/>), runs the target through
/// it, and reads the files the target writes. Evaluation happens in a process of its own, with the
/// repository's own SDK: the same view a build has, which is the only view that agrees with what the
/// repository actually builds.</para>
/// <para>Evaluating in process, through MSBuild's own libraries, was considered and rejected. Evaluating a
/// project loads the NuGet assemblies the repository's SDK carries into the same load context as the ones
/// <c>bv</c> uses to talk to package sources, and one simple name means one assembly: which of the two wins
/// would depend on the SDK a repository pins.</para>
/// <para>The invocation is bv's own, so it takes none of the arguments and environment variables the
/// configuration file states for <c>dotnet</c> commands: those describe builds, and would be rejected by
/// <c>dotnet msbuild</c> more often than not.</para>
/// </remarks>
internal sealed class SolutionTargetRunner(IHomeDirectoryProvider home, IProcessRunner processRunner, IReporter reporter)
{
    private const string DriverProjectFileName = "driver.proj";

    /// <summary>
    /// Runs a target on every project of a solution and returns what it wrote.
    /// </summary>
    /// <typeparam name="T">The type of the content of each file the target writes.</typeparam>
    /// <param name="target">The target to run.</param>
    /// <param name="solution">The solution whose projects the target runs on.</param>
    /// <param name="properties">The global properties of the run, besides the one that names the output
    /// directory and the ones a solution build gives every project.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>The content of each file the target wrote, in no particular order. A project the Buildvana SDK
    /// does not reach contributes none.</returns>
    /// <exception cref="BuildFailedException">The evaluation failed, or a file could not be read.</exception>
    public async Task<IReadOnlyList<T>> RunAsync<T>(
        SolutionTarget<T> target,
        SolutionContext solution,
        IReadOnlyDictionary<string, string> properties,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(target);
        Guard.IsNotNull(solution);
        Guard.IsNotNull(properties);
        var projectPaths = solution.ProjectPaths;
        if (projectPaths.Count == 0)
        {
            return [];
        }

        var directory = home.GetFullPath(target.OutputDirectory);
        var driverPath = Path.Combine(directory, DriverProjectFileName);
        PrepareDirectory(directory);
        WriteFile(driverPath, SolutionDriverProject.Create(projectPaths, target.Name));
        string[] propertyArgs = [
            $"-property:{target.OutputDirectoryProperty}={directory}",
            .. SolutionProperties(solution).Select(static pair => $"-property:{pair.Key}={pair.Value}"),
            .. properties.Select(static pair => $"-property:{pair.Key}={pair.Value}"),
        ];

        await RunAsync(driverPath, propertyArgs, cancellationToken).ConfigureAwait(false);
        return ReadFiles(directory, target);
    }

    // The directory holds one file per evaluation of the last run, so it starts empty: a project removed
    // from the solution, or a target framework dropped from a project, would otherwise keep being read.
    private static void PrepareDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            _ = Directory.CreateDirectory(directory);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not prepare '{directory}': {e.Message}", e);
        }
    }

    private static void WriteFile(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException($"Could not write '{path}': {e.Message}", e);
        }
    }

    // A solution build gives every project these global properties, and a project may derive its output paths
    // from them, as in `$(SolutionDir)bin/`. Without them, the evaluation would state paths the build never
    // wrote. Names and values follow AddGlobalProperties in MSBuild's SolutionProjectGenerator, where
    // SolutionDir ends with a directory separator.
    private static Dictionary<string, string> SolutionProperties(SolutionContext solution)
    {
        var directory = solution.SolutionDirectory;
        return new() {
            ["SolutionDir"] = Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar,
            ["SolutionExt"] = Path.GetExtension(solution.SolutionPath),
            ["SolutionFileName"] = Path.GetFileName(solution.SolutionPath),
            ["SolutionName"] = Path.GetFileNameWithoutExtension(solution.SolutionPath),
            ["SolutionPath"] = solution.SolutionPath,
        };
    }

    private static List<T> ReadFiles<T>(string directory, SolutionTarget<T> target)
    {
        var contents = new List<T>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            contents.Add(ReadFile(path, target));
        }

        return contents;
    }

    // A file is written by the SDK the tool checked before running, so a file it cannot read means the two
    // disagree about the contract, not that the user did something wrong.
    private static T ReadFile<T>(string path, SolutionTarget<T> target)
    {
        try
        {
            var content = File.ReadAllText(path);
            return JsonSerializer.Deserialize(content, target.TypeInfo)
                ?? throw new BuildFailedException(
                    ExitCodes.ExternalProgramFailed,
                    $"The {target.ContentDescription} in '{path}' read as nothing.");
        }
        catch (JsonException e)
        {
            throw new BuildFailedException(
                ExitCodes.ExternalProgramFailed,
                $"Could not read the {target.ContentDescription} in '{path}': {e.Message}",
                e);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException(ExitCodes.ExternalProgramFailed, $"Could not read '{path}': {e.Message}", e);
        }
    }

    private async Task RunAsync(string driverPath, IReadOnlyList<string> propertyArgs, CancellationToken cancellationToken)
    {
        string[] args = [
            "msbuild",
            driverPath,
            "-nologo",
            "-maxCpuCount",
            $"-target:{SolutionDriverProject.TargetName}",
            .. propertyArgs,
            $"-verbosity:{reporter.Verbosity}",
        ];

        var result = await processRunner.RunAsync(
            DotNetMuxer.Path,
            args,
            throwOnNonZero: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return;
        }

        // MSBuild writes its errors to standard output. They are the whole explanation of the failure, so
        // they are reported whatever the verbosity, in the form a terminal renders as clickable locations.
        foreach (var line in result.StandardOutput.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            reporter.ChildError(line, Verbosity.Quiet);
        }

        throw new BuildFailedException(
            ExitCodes.ExternalProgramFailed,
            $"MSBuild could not evaluate the solution's projects (exit code {result.ExitCode}).");
    }
}
