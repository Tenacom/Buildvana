// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Core.Diagnostics;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.Process;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the package items of a solution's projects, as MSBuild evaluates them.
/// </summary>
/// <remarks>
/// <para>The reader writes a driver project (see <see cref="PinDumpDriverProject"/>), runs the SDK's pin
/// dump target through it, and reads the files the target writes. Evaluation happens in a process of its
/// own, with the repository's own SDK: the same view a build has, which is the only view that agrees with
/// what the repository actually resolves.</para>
/// <para>Evaluating in process, through MSBuild's own libraries, was considered and rejected. Evaluating a
/// project loads the NuGet assemblies the repository's SDK carries into the same load context as the ones
/// <c>bv</c> uses to talk to package sources, and one simple name means one assembly: which of the two wins
/// would depend on the SDK a repository pins.</para>
/// <para>The invocation is bv's own, so it takes none of the arguments and environment variables the
/// configuration file states for <c>dotnet</c> commands: those describe builds, and would be rejected by
/// <c>dotnet msbuild</c> more often than not.</para>
/// </remarks>
internal sealed class SolutionPinReader(IHomeDirectoryProvider home, IProcessRunner processRunner, IReporter reporter)
{
    private const string DriverProjectFileName = "pin-dump.proj";

    /// <summary>
    /// Runs the pin dump target on the given projects and returns what it wrote.
    /// </summary>
    /// <param name="projectPaths">The full paths of the solution's projects.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>One dump per evaluation: one per project, or one per target framework of a multi-targeting
    /// project. A project the Buildvana SDK does not reach contributes none.</returns>
    /// <exception cref="BuildFailedException">The evaluation failed, or a dump could not be read.</exception>
    public async Task<IReadOnlyList<PackagePinDump>> ReadAsync(
        IReadOnlyList<string> projectPaths,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(projectPaths);
        if (projectPaths.Count == 0)
        {
            return [];
        }

        var directory = home.GetFullPath(CommonPaths.PinDump);
        var driverPath = Path.Combine(directory, DriverProjectFileName);
        PrepareDirectory(directory);
        WriteFile(driverPath, PinDumpDriverProject.Create(projectPaths));
        await RunAsync(driverPath, directory, cancellationToken).ConfigureAwait(false);
        var dumps = ReadDumps(directory);

        // A project whose SDK does not define the dump target is skipped rather than fatal, which is what
        // lets a solution hold projects the Buildvana SDK never sees. When no project at all answered, the
        // silence is worth a word: a report of no package pins would otherwise read as a repository with
        // none.
        if (dumps.Count == 0)
        {
            reporter.Warning(
                "No project of the solution answered the package pin dump, so no package pins were read. "
                + "The Buildvana SDK the repository pins may predate it.");
        }

        return dumps;
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
            throw new BuildFailedException(ExitCodes.StepFailed, $"Could not prepare '{directory}': {e.Message}", e);
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
            throw new BuildFailedException(ExitCodes.StepFailed, $"Could not write '{path}': {e.Message}", e);
        }
    }

    private static List<PackagePinDump> ReadDumps(string directory)
    {
        var dumps = new List<PackagePinDump>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            dumps.Add(ReadDump(path));
        }

        return dumps;
    }

    // A dump is written by the SDK the tool checked before running, so a file it cannot read means the two
    // disagree about the contract, not that the user did something wrong.
    private static PackagePinDump ReadDump(string path)
    {
        try
        {
            var content = File.ReadAllText(path);
            return JsonSerializer.Deserialize(content, PackagePinDumpJsonContext.Default.PackagePinDump)
                ?? throw new BuildFailedException(ExitCodes.StepFailed, $"The package pins in '{path}' read as nothing.");
        }
        catch (JsonException e)
        {
            throw new BuildFailedException(ExitCodes.StepFailed, $"Could not read the package pins in '{path}': {e.Message}", e);
        }
        catch (Exception e) when (e.IsIORelatedException)
        {
            throw new BuildFailedException(ExitCodes.StepFailed, $"Could not read '{path}': {e.Message}", e);
        }
    }

    private async Task RunAsync(string driverPath, string directory, CancellationToken cancellationToken)
    {
        reporter.Info("Reading the solution's package pins...");
        string[] args = [
            "msbuild",
            driverPath,
            "-nologo",
            "-maxCpuCount",
            $"-target:{PinDumpDriverProject.TargetName}",
            $"-property:BV_PinDumpDirectory={directory}",
            "-property:BV_SuppressTransitiveOverrides=true",
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
            ExitCodes.StepFailed,
            $"MSBuild could not evaluate the solution's projects (exit code {result.ExitCode}).");
    }
}
