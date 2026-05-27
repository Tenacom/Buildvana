// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Buildvana.Tool.CommandLine;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Buildvana.Tool.Build;

/// <summary>
/// The ordered build pipeline (<c>Clean → Restore → Build → Test → Pack</c>). Owns the body of each
/// <see cref="BuildStep"/> and exposes step-level execution: a single step, a prefix from <see cref="BuildStep.Clean"/>,
/// or an arbitrary contiguous range.
/// </summary>
internal sealed class BuildPipeline
{
    /// <summary>
    /// The MSBuild configuration used when a caller does not specify one. It is emitted as an overridable
    /// default, so a user-forwarded <c>-c</c>/<c>-p:Configuration=</c> wins.
    /// </summary>
    public const string DefaultConfiguration = "Release";

    private readonly SolutionContext _solution;
    private readonly DotNetService _dotnet;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IReadOnlyList<string> _forwardedArgs;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildPipeline"/> class.
    /// </summary>
    public BuildPipeline(
        SolutionContext solution,
        DotNetService dotnet,
        ILoggerFactory loggerFactory,
        CommandParameters parameters)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNull(dotnet);
        Guard.IsNotNull(loggerFactory);
        Guard.IsNotNull(parameters);
        _solution = solution;
        _dotnet = dotnet;
        _loggerFactory = loggerFactory;
        _forwardedArgs = parameters.Forwarded;
    }

    /// <summary>
    /// Runs the pipeline from <see cref="BuildStep.Clean"/> through <paramref name="last"/>, inclusive.
    /// </summary>
    /// <param name="last">The last step to run.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task RunThroughAsync(BuildStep last, string configuration = DefaultConfiguration)
        => RunRangeAsync(BuildStep.Clean, last, configuration);

    /// <summary>
    /// Runs the pipeline from <paramref name="first"/> through <paramref name="last"/>, inclusive.
    /// </summary>
    /// <param name="first">The first step to run.</param>
    /// <param name="last">The last step to run.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task RunRangeAsync(BuildStep first, BuildStep last, string configuration = DefaultConfiguration)
    {
        Guard.IsLessThanOrEqualTo((int)first, (int)last, nameof(first));
        for (var step = first; step <= last; step++)
        {
            await RunAsync(step, configuration).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a single pipeline step.
    /// </summary>
    /// <param name="step">The step to run.</param>
    /// <param name="configuration">The MSBuild configuration to build (ignored by <see cref="BuildStep.Clean"/> and <see cref="BuildStep.Restore"/>).</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task RunAsync(BuildStep step, string configuration = DefaultConfiguration)
        => step switch
        {
            BuildStep.Clean => CleanAsync(),
            BuildStep.Restore => RestoreAsync(),
            BuildStep.Build => BuildAsync(configuration),
            BuildStep.Test => TestAsync(configuration),
            BuildStep.Pack => PackAsync(configuration),
            _ => ThrowHelper.ThrowArgumentOutOfRangeException<Task>(nameof(step), step, "Unknown build step."),
        };

    private Task CleanAsync()
    {
        var logger = _loggerFactory.CreateLogger("Clean");
        FileSystemHelper.DeleteDirectory(_solution.ResolvePath(".vs"), logger);
        FileSystemHelper.DeleteDirectory(_solution.ResolvePath("_ReSharper.Caches"), logger);
        FileSystemHelper.DeleteDirectory(_solution.ResolvePath("temp"), logger);
        FileSystemHelper.DeleteDirectory(_solution.ResolvePath(CommonPaths.AllArtifacts), logger);
        FileSystemHelper.DeleteDirectory(_solution.ResolvePath(CommonPaths.TestResults), logger);
        foreach (var project in _solution.Model.SolutionProjects)
        {
            var projectDirectory = Path.GetDirectoryName(_solution.ResolveProjectPath(project))!;
            FileSystemHelper.DeleteDirectory(Path.Combine(projectDirectory, "bin"), logger);
            FileSystemHelper.DeleteDirectory(Path.Combine(projectDirectory, "obj"), logger);
        }

        return Task.CompletedTask;
    }

    private Task RestoreAsync()
        => _dotnet.RestoreSolutionAsync(_solution, _forwardedArgs);

    private Task BuildAsync(string configuration)
        => _dotnet.BuildSolutionAsync(_solution, configuration, _forwardedArgs, restore: false);

    private Task TestAsync(string configuration)
        => _dotnet.TestSolutionAsync(_solution, configuration, _forwardedArgs, restore: false, build: false);

    private Task PackAsync(string configuration)
        => _dotnet.PackSolutionAsync(_solution, configuration, _forwardedArgs, restore: false, build: false);
}
