// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Dependencies;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.Solution;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the package items of a solution's projects, as MSBuild evaluates them.
/// </summary>
/// <remarks>
/// <para>The reader runs the SDK's pin dump target on every project of the solution, through
/// <see cref="SolutionTargetRunner"/>, and reads the files the target writes.</para>
/// </remarks>
internal sealed class SolutionPinReader(SolutionTargetRunner runner, IReporter reporter)
{
    private static readonly SolutionTarget<PackagePinDump> Target = new(
        Name: "BV_DumpPackagePins",
        OutputDirectory: CommonPaths.PinDump,
        OutputDirectoryProperty: "BV_PinDumpDirectory",
        ContentDescription: "package pins",
        TypeInfo: PackagePinDumpJsonContext.Default.PackagePinDump);

    private static readonly Dictionary<string, string> Properties = new() {
        [TransitiveOverrides.SuppressionProperty] = "true",
    };

    /// <summary>
    /// Runs the pin dump target on every project of a solution and returns what it wrote.
    /// </summary>
    /// <param name="solution">The solution whose projects the target runs on.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>One dump per evaluation: one per project, or one per target framework of a multi-targeting
    /// project. A project the Buildvana SDK does not reach contributes none.</returns>
    /// <exception cref="BuildFailedException">The evaluation failed, or a dump could not be read.</exception>
    public async Task<IReadOnlyList<PackagePinDump>> ReadAsync(
        SolutionContext solution,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        if (solution.Model.SolutionProjects.Count == 0)
        {
            return [];
        }

        reporter.Info("Reading the solution's package pins...");
        var dumps = await runner.RunAsync(Target, solution, Properties, cancellationToken).ConfigureAwait(false);

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
}
