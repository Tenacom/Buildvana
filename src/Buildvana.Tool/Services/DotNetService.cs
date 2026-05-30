// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Tool.Configuration;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Subcommands;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using IProcessRunner = Buildvana.Core.Process.IProcessRunner;
using ProcessResult = Buildvana.Core.Process.ProcessResult;

namespace Buildvana.Tool.Services;

/// <summary>
/// Provides shortcut methods for .NET SDK operations.
/// </summary>
internal sealed class DotNetService
{
    // The muxer sets DOTNET_HOST_PATH to the full path of the dotnet executable that launched us,
    // so we re-invoke that exact host instead of relying on `dotnet` being on PATH.
    private static readonly string DotNetMuxer
        = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p
            ? p
            : "dotnet";

    private readonly IReporter _reporter;
    private readonly IProcessRunner _processRunner;
    private readonly IServiceProvider _services;
    private readonly ServerAdapter _server;
    private readonly VersionService _version;
    private readonly GlobalSettings _globals;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetService"/> class.
    /// </summary>
    public DotNetService(
        IReporter reporter,
        IProcessRunner processRunner,
        IServiceProvider services,
        ServerAdapter server,
        VersionService version,
        GlobalSettings globals)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(processRunner);
        Guard.IsNotNull(services);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);
        Guard.IsNotNull(globals);
        _reporter = reporter;
        _processRunner = processRunner;
        _services = services;
        _server = server;
        _version = version;
        _globals = globals;
    }

    // The verbosity bv forwards to every `dotnet` invocation. bv and the .NET CLI share the same vocabulary
    // (quiet/minimal/normal/detailed/diagnostic and their short forms), so the raw value is forwarded as-is.
    private string Verbosity => _globals.Verbosity ?? "normal";

    /// <summary>
    /// Asynchronously restores all NuGet packages for the solution.
    /// </summary>
    /// <param name="solution">The solution to restore.</param>
    /// <param name="forwardedArgs">Extra arguments to forward verbatim to the <c>dotnet</c> invocation.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task RestoreSolutionAsync(SolutionContext solution, IReadOnlyList<string> forwardedArgs, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNull(forwardedArgs);
        _reporter.Info("Restoring NuGet packages for solution...");
        List<string> args = ["restore", solution.SolutionPath, "--disable-parallel", "-nologo", "-v", Verbosity];
        args.AddRange(forwardedArgs);
        args.Add(ContinuousIntegrationBuildArg(dotnetTest: false));
        return RunDotNetAsync(args, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously builds all projects in the solution.
    /// </summary>
    /// <param name="solution">The solution to build.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <param name="forwardedArgs">Extra arguments to forward verbatim to the <c>dotnet</c> invocation.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before building, <see langword="false"/> otherwise.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task BuildSolutionAsync(SolutionContext solution, string configuration, IReadOnlyList<string> forwardedArgs, bool restore, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNullOrEmpty(configuration);
        Guard.IsNotNull(forwardedArgs);
        _reporter.Info($"Building solution (restore = {restore})...");
        List<string> args = ["build", solution.SolutionPath, "-nologo", "-v", Verbosity, $"-p:Configuration={configuration}"];
        if (!restore)
        {
            args.Add("--no-restore");
        }

        args.AddRange(forwardedArgs);
        args.Add(ContinuousIntegrationBuildArg(dotnetTest: false));
        return RunDotNetAsync(args, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously runs all tests for the solution.
    /// </summary>
    /// <param name="solution">The solution to test.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <param name="forwardedArgs">Extra arguments to forward verbatim to the <c>dotnet test</c> invocation
    /// (reaching the Microsoft.Testing.Platform test applications).</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before testing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before testing, <see langword="false"/> otherwise.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task TestSolutionAsync(SolutionContext solution, string configuration, IReadOnlyList<string> forwardedArgs, bool restore, bool build, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNullOrEmpty(configuration);
        Guard.IsNotNull(forwardedArgs);
        _reporter.Info("Checking for MTP test projects...");
        var hasTestProjects = false;
        foreach (var project in solution.Model.SolutionProjects)
        {
            var projectPath = solution.ResolveProjectPath(project);
            _reporter.Detail($"Checking '{projectPath}'...");

            // bv-internal MSBuild evaluation: do not forward the user's arguments here, as they may be
            // test-application options that `dotnet msbuild` would reject.
            List<string> probeArgs = ["msbuild", projectPath, "-nologo", "-getProperty:IsTestingPlatformApplication"];
            var probe = await RunDotNetAsync(probeArgs, streamOutput: false, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (string.Equals(probe.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                _reporter.Detail($"Project '{projectPath}' is a test project, will run tests.");
                hasTestProjects = true;
                break;
            }
        }

        if (!hasTestProjects)
        {
            _reporter.Info("No test projects found, skipping tests.");
            return;
        }

        _reporter.Info($"Running tests (restore = {restore}, build = {build})...");

        // `dotnet test` consumes --verbosity itself; the configuration and ContinuousIntegrationBuild are
        // passed as MSBuild properties using the `--property:` form, which is what `dotnet test` understands
        // (the `-p:` form is not supported here).
        List<string> args = ["test", solution.SolutionPath, $"--verbosity={Verbosity}", $"--property:Configuration={configuration}"];
        if (!build)
        {
            args.Add("--no-build");
        }

        if (!restore)
        {
            args.Add("--no-restore");
        }

        args.AddRange(["--coverage", "--coverage-output-format", "cobertura", "--results-directory", CommonPaths.TestResults]);
        args.AddRange(forwardedArgs);
        args.Add(ContinuousIntegrationBuildArg(dotnetTest: true));
        await RunDotNetAsync(args, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously runs the Pack target on the solution. This usually produces NuGet packages, but Buildvana SDK may hijack the target to produce, for example, setup executables.
    /// </summary>
    /// <param name="solution">The solution to pack.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <param name="forwardedArgs">Extra arguments to forward verbatim to the <c>dotnet</c> invocation.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before packing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before packing, <see langword="false"/> otherwise.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task PackSolutionAsync(SolutionContext solution, string configuration, IReadOnlyList<string> forwardedArgs, bool restore, bool build, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNullOrEmpty(configuration);
        Guard.IsNotNull(forwardedArgs);
        _reporter.Info($"Packing solution (restore = {restore}, build = {build})...");
        List<string> args = ["pack", solution.SolutionPath, "-nologo", "-v", Verbosity, $"-p:Configuration={configuration}"];
        if (!build)
        {
            args.Add("--no-build");
        }

        if (!restore)
        {
            args.Add("--no-restore");
        }

        args.AddRange(forwardedArgs);
        args.Add(ContinuousIntegrationBuildArg(dotnetTest: false));
        return RunDotNetAsync(args, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously pushes all produced NuGet packages to the appropriate NuGet server.
    /// </summary>
    /// <param name="artifactsPath">The path of the directory containing the produced <c>*.nupkg</c> files.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task NuGetPushAllAsync(string artifactsPath, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(artifactsPath);
        var packages = FileSystemHelper.EnumerateFiles(artifactsPath, "*.nupkg").ToArray();
        if (packages.Length == 0)
        {
            _reporter.Detail("No .nupkg files to push.");
            return;
        }

        var isPrivate = await _server.IsPrivateRepositoryAsync().ConfigureAwait(false);
        var nugetConfig = _services.GetRequiredService<NuGetPushConfiguration>();
        var target = isPrivate ? nugetConfig.Private
            : _version.IsPrerelease ? nugetConfig.Prerelease
            : nugetConfig.Release;
        foreach (var path in packages)
        {
            _reporter.Info($"Pushing {path} to {target.Source}...");
            await _processRunner
                .RunAsync(
                    DotNetMuxer,
                    ["nuget", "push", path, "--source", target.Source, "--api-key", target.ApiKey, "--skip-duplicate", "--force-english-output"],
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // bv's authoritative ContinuousIntegrationBuild value, emitted in the trailing group so it wins under
    // MSBuild's last-wins resolution. `dotnet test` requires the `--property:` form; the other verbs accept `-p:`.
    private string ContinuousIntegrationBuildArg(bool dotnetTest)
    {
        var prefix = dotnetTest ? "--property:" : "-p:";
        return $"{prefix}ContinuousIntegrationBuild={(_server.IsCloudBuild ? "true" : "false")}";
    }

    // streamOutput is false for bv-internal evaluations (e.g. the MTP probe) whose output is captured and
    // inspected rather than shown to the user; user-facing verbs stream the child's output live.
    private Task<ProcessResult> RunDotNetAsync(
        IEnumerable<string> args,
        bool streamOutput = true,
        CancellationToken cancellationToken = default)
        => _processRunner.RunAsync(
            DotNetMuxer,
            args,
            onStdout: streamOutput ? (x) => _reporter.ChildOutput(x, null) : null,
            onStderr: streamOutput ? (x) => _reporter.ChildError(x, null) : null,
            cancellationToken: cancellationToken);
}
