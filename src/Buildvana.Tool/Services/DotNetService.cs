// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;

using IProcessRunner = Buildvana.Core.Process.IProcessRunner;
using ProcessResult = Buildvana.Core.Process.ProcessResult;

namespace Buildvana.Tool.Services;

/// <summary>
/// Provides shortcut methods for .NET SDK operations.
/// </summary>
public sealed class DotNetService
{
    // The muxer sets DOTNET_HOST_PATH to the full path of the dotnet executable that launched us,
    // so we re-invoke that exact host instead of relying on `dotnet` being on PATH.
    private static readonly string DotNetMuxer
        = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p
            ? p
            : "dotnet";

    private readonly ILogger<DotNetService> _logger;
    private readonly IProcessRunner _processRunner;
    private readonly OptionsService _options;
    private readonly ServerAdapter _server;
    private readonly VersionService _version;
    private readonly MSBuildProperties _msbuildProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetService"/> class.
    /// </summary>
    public DotNetService(
        ILogger<DotNetService> logger,
        IProcessRunner processRunner,
        OptionsService options,
        ServerAdapter server,
        VersionService version,
        MSBuildProperties msbuildProperties)
    {
        Guard.IsNotNull(logger);
        Guard.IsNotNull(processRunner);
        Guard.IsNotNull(options);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);
        Guard.IsNotNull(msbuildProperties);
        _logger = logger;
        _processRunner = processRunner;
        _options = options;
        _server = server;
        _version = version;
        _msbuildProperties = msbuildProperties;
        Configuration = options.GetOption("configuration", "Release");
        ArtifactsPath = Path.Combine(CommonPaths.AllArtifacts, Configuration);
    }

    /// <summary>
    /// Gets the configuration to build.
    /// </summary>
    public string Configuration { get; }

    /// <summary>
    /// Gets the path of the directory where build artifacts for <see cref="Configuration"/> are stored.
    /// </summary>
    public string ArtifactsPath { get; }

    /// <summary>
    /// Asynchronously restores all NuGet packages for the solution.
    /// </summary>
    /// <param name="solution">The solution to restore.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task RestoreSolutionAsync(SolutionContext solution)
    {
        Guard.IsNotNull(solution);
        _logger.LogInformation("Restoring NuGet packages for solution...");
        List<string> args = ["restore", solution.SolutionPath, "--disable-parallel"];
        args.AddRange(StandardMSBuildArgs());
        return RunDotNetAsync(args);
    }

    /// <summary>
    /// Asynchronously builds all projects in the solution.
    /// </summary>
    /// <param name="solution">The solution to build.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before building, <see langword="false"/> otherwise.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task BuildSolutionAsync(SolutionContext solution, bool restore)
    {
        Guard.IsNotNull(solution);
        _logger.LogInformation("Building solution (restore = {Restore})...", restore);
        List<string> args = ["build", solution.SolutionPath, "-c", Configuration];
        if (!restore)
        {
            args.Add("--no-restore");
        }

        args.AddRange(StandardMSBuildArgs());
        return RunDotNetAsync(args);
    }

    /// <summary>
    /// Asynchronously runs all tests for the solution.
    /// </summary>
    /// <param name="solution">The solution to test.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before testing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before testing, <see langword="false"/> otherwise.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task TestSolutionAsync(SolutionContext solution, bool restore, bool build)
    {
        Guard.IsNotNull(solution);
        _logger.LogInformation("Checking for MTP test projects...");
        var hasTestProjects = false;
        foreach (var project in solution.Model.SolutionProjects)
        {
            var projectPath = solution.ResolveProjectPath(project);
            _logger.LogDebug("Checking '{Path}'...", projectPath);
            List<string> probeArgs = ["msbuild", projectPath];
            probeArgs.AddRange(StandardMSBuildArgs());
            probeArgs.Add("-getProperty:IsTestingPlatformApplication");
            var probe = await RunDotNetAsync(probeArgs).ConfigureAwait(false);

            if (string.Equals(probe.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Project '{Path}' is a test project, will run tests.", projectPath);
                hasTestProjects = true;
                break;
            }
        }

        if (!hasTestProjects)
        {
            _logger.LogInformation("No test projects found, skipping tests.");
            return;
        }

        _logger.LogInformation("Running tests (restore = {Restore}, build = {Build})...", restore, build);

        // Don't pass the standard MSBuild args here: `dotnet test` forwards them to MTP applications,
        // which (at least those built with TUnit) fail because they don't recognize -nologo and -maxcpucount.
        List<string> args = ["test", solution.SolutionPath, "-c", Configuration, ContinuousIntegrationBuildArg()];
        if (!build)
        {
            args.Add("--no-build");
        }

        if (!restore)
        {
            args.Add("--no-restore");
        }

        args.AddRange(["--coverage", "--coverage-output-format", "cobertura", "--results-directory", CommonPaths.TestResults]);

        // `dotnet test` doesn't support passing MSBuild properties with `-p:Key=Value`, nor does it support passing any other parameter to MSBuild.
        // It _does_ support `--property:Key=Value`, though. We need to pass MSBuild properties differently than in the other commands, but we can (and must) do so.
        args.AddRange(_msbuildProperties.EnumerateAsDotnetTestArgs());
        await RunDotNetAsync(args).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously runs the Pack target on the solution. This usually produces NuGet packages, but Buildvana SDK may hijack the target to produce, for example, setup executables.
    /// </summary>
    /// <param name="solution">The solution to pack.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before packing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before packing, <see langword="false"/> otherwise.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task PackSolutionAsync(SolutionContext solution, bool restore, bool build)
    {
        Guard.IsNotNull(solution);
        _logger.LogInformation("Packing solution (restore = {Restore}, build = {Build})...", restore, build);
        List<string> args = ["pack", solution.SolutionPath, "-c", Configuration];
        if (!build)
        {
            args.Add("--no-build");
        }

        if (!restore)
        {
            args.Add("--no-restore");
        }

        args.AddRange(StandardMSBuildArgs());
        return RunDotNetAsync(args);
    }

    /// <summary>
    /// Asynchronously pushes all produced NuGet packages to the appropriate NuGet server.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task NuGetPushAllAsync()
    {
        var packages = FileSystemHelper.EnumerateFiles(ArtifactsPath, "*.nupkg").ToArray();
        if (packages.Length == 0)
        {
            _logger.LogDebug("No .nupkg files to push.");
            return;
        }

        var isPrivate = await _server.IsPrivateRepositoryAsync().ConfigureAwait(false);
        var nugetSource = _options.GetOptionOrFail<string>(isPrivate ? "privateNugetSource" : _version.IsPrerelease ? "prereleaseNugetSource" : "releaseNugetSource");
        var nugetApiKey = _options.GetOptionOrFail<string>(isPrivate ? "privateNugetKey" : _version.IsPrerelease ? "prereleaseNugetKey" : "releaseNugetKey");
        foreach (var path in packages)
        {
            _logger.LogInformation("Pushing {Path} to {Source}...", path, nugetSource);
            await _processRunner
                .RunAsync(
                    DotNetMuxer,
                    ["nuget", "push", path, "--source", nugetSource, "--api-key", nugetApiKey, "--skip-duplicate", "--force-english-output"])
                .ConfigureAwait(false);
        }
    }

    private string ContinuousIntegrationBuildArg()
        => $"-p:ContinuousIntegrationBuild={(_server.IsCloudBuild ? "true" : "false")}";

    private IEnumerable<string> StandardMSBuildArgs()
    {
        yield return "-nologo";
        yield return "-maxcpucount:1";
        yield return ContinuousIntegrationBuildArg();
        foreach (var arg in _msbuildProperties.EnumerateAsArgs())
        {
            yield return arg;
        }
    }

    private Task<ProcessResult> RunDotNetAsync(IEnumerable<string> args)
        => _processRunner.RunAsync(DotNetMuxer, args);
}
