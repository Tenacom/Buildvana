// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.IO;
using Buildvana.Core.Process;
using Buildvana.Runtime;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Infrastructure.Delegation;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Provides shortcut methods for .NET SDK operations.
/// </summary>
internal sealed partial class DotNetService : IFileBasedAppRunner
{
    private readonly IReporter _reporter;
    private readonly IProcessRunner _processRunner;
    private readonly ServerAdapter _server;
    private readonly VersionService _version;
    private readonly BuildvanaConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetService"/> class.
    /// </summary>
    public DotNetService(
        IReporter reporter,
        IProcessRunner processRunner,
        ServerAdapter server,
        VersionService version,
        BuildvanaConfig config)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(processRunner);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);
        Guard.IsNotNull(config);
        _reporter = reporter;
        _processRunner = processRunner;
        _server = server;
        _version = version;
        _config = config;
    }

    /// <summary>
    /// Asynchronously restores all NuGet packages for the solution.
    /// </summary>
    /// <param name="solution">The solution to restore.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task RestoreSolutionAsync(SolutionContext solution, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        _reporter.Info("Restoring NuGet packages for solution...");
        string[] args = [
            "restore",
            solution.SolutionPath,
            "--disable-parallel",
            "-nologo",
        ];

        return RunDotNetAsync(
            args,
            invocation: _config.DotNet.Restore,
            trailingArgs: [ContinuousIntegrationBuildArg(asMSBuildPassthrough: true)],
            appendVerbosity: true,
            outputStreaming: OutputStreaming.Unconditional,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously builds all projects in the solution.
    /// </summary>
    /// <param name="solution">The solution to build.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before building, <see langword="false"/> otherwise.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task BuildSolutionAsync(
        SolutionContext solution,
        string configuration,
        bool restore,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNullOrEmpty(configuration);
        _reporter.Info($"Building solution (restore = {restore})...");
        string[] args = [
            "build",
            solution.SolutionPath,
            "-nologo",
            $"-p:Configuration={configuration}",
            .. restore ? Array.Empty<string>() : ["--no-restore"],
        ];

        return RunDotNetAsync(
            args,
            invocation: _config.DotNet.Build,
            trailingArgs: [ContinuousIntegrationBuildArg(asMSBuildPassthrough: true)],
            appendVerbosity: true,
            outputStreaming: OutputStreaming.Unconditional,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Asynchronously runs all tests for the solution.
    /// </summary>
    /// <param name="solution">The solution to test.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before testing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before testing, <see langword="false"/> otherwise.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task TestSolutionAsync(
        SolutionContext solution,
        string configuration,
        bool restore,
        bool build,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNullOrEmpty(configuration);
        _reporter.Info("Checking for MTP test projects...");
        var hasTestProjects = false;
        foreach (var project in solution.Model.SolutionProjects)
        {
            var projectPath = solution.ResolveProjectPath(project);
            _reporter.Detail($"Checking '{projectPath}'...");

            // bv-internal MSBuild evaluation: run it bare, with none of the configured or forwarded
            // arguments, as they may be options `dotnet msbuild` would reject.
            string[] probeArgs = ["msbuild", projectPath, "-nologo", "-getProperty:IsTestingPlatformApplication"];
            var probe = await RunDotNetAsync(
                probeArgs,
                invocation: new(),
                trailingArgs: [],
                appendVerbosity: false,
                outputStreaming: OutputStreaming.Disabled,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (string.Equals(probe.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                _reporter.Detail($"Project '{projectPath}' is a test project, will run tests.");
                hasTestProjects = true;
                break;
            }
        }

        if (!hasTestProjects)
        {
            _reporter.Notice("No test projects found, skipping tests.");
            return;
        }

        _reporter.Info($"Running tests (restore = {restore}, build = {build})...");

        // `dotnet test` consumes --verbosity itself; the configuration and ContinuousIntegrationBuild are
        // passed as MSBuild properties using the `--property:` form, which is what `dotnet test` understands
        // (the `-p:` form is not supported here).
        string[] args = [
            "test",
            solution.SolutionPath,
            $"--property:Configuration={configuration}",
            .. restore ? Array.Empty<string>() : ["--no-restore"],
            .. build ? Array.Empty<string>() : ["--no-build"],
            "--results-directory",
            CommonPaths.TestResults,
            "--output",
            _reporter.IsVerbosityAtLeast(Verbosity.Detailed) ? "Detailed" : "Normal",
        ];

        await RunDotNetAsync(
            args,
            invocation: _config.DotNet.Test,
            trailingArgs: [ContinuousIntegrationBuildArg(asMSBuildPassthrough: false)],
            appendVerbosity: true,
            outputStreaming: OutputStreaming.Unconditional,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously runs the Pack target on the solution. This usually produces NuGet packages, but
    /// Buildvana SDK may hijack the target to produce, for example, setup executables.
    /// </summary>
    /// <param name="solution">The solution to pack.</param>
    /// <param name="configuration">The MSBuild configuration to build.</param>
    /// <param name="restore"><see langword="true"/> to restore NuGet packages before packing, <see langword="false"/> otherwise.</param>
    /// <param name="build"><see langword="true"/> to build the solution before packing, <see langword="false"/> otherwise.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public Task PackSolutionAsync(
        SolutionContext solution,
        string configuration,
        bool restore,
        bool build,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(solution);
        Guard.IsNotNullOrEmpty(configuration);
        _reporter.Info($"Packing solution (restore = {restore}, build = {build})...");
        string[] args = [
            "pack",
            solution.SolutionPath,
            "-nologo",
            $"-p:Configuration={configuration}",
            .. restore ? Array.Empty<string>() : ["--no-restore"],
            .. build ? Array.Empty<string>() : ["--no-build"],
        ];

        return RunDotNetAsync(
            args,
            invocation: _config.DotNet.Pack,
            trailingArgs: [ContinuousIntegrationBuildArg(asMSBuildPassthrough: true)],
            appendVerbosity: true,
            outputStreaming: OutputStreaming.Unconditional,
            cancellationToken: cancellationToken);
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
        var packages = UserDirectory.EnumerateFiles(artifactsPath, "*.nupkg").ToArray();
        if (packages.Length == 0)
        {
            _reporter.Detail("No .nupkg files to push.");
            return;
        }

        var feed = ResolvePushFeed(_config.NuGet.Feeds, _version.IsPrerelease);
        var apiKey = RuntimeAccess.Translate(feed.GetApiKey);

        foreach (var path in packages)
        {
            _reporter.Detail($"Pushing {path} to {feed.Source}...");
            string[] args = [

                // `dotnet nuget` has no verbosity option; use the global diagnostics flag when diagnostic output is enabled.
                .. _reporter.IsVerbosityAtLeast(Verbosity.Diagnostic) ? ["-d"] : Array.Empty<string>(),
                "nuget",
                "push",
                path,
                "--source",
                feed.Source,
                "--api-key",
                apiKey,
                "--skip-duplicate",
            ];
            await RunDotNetAsync(
                args,
                invocation: _config.DotNet.NugetPush,
                trailingArgs: [],
                appendVerbosity: false,
                outputStreaming: OutputStreaming.AtVerbosity(Verbosity.Normal),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        _reporter.Notice($"Pushed {packages.Length} packages to {feed.Source}.");
    }

    /// <inheritdoc/>
    public Task<ProcessResult> RunFileBasedAppAsync(
        string path,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(path);

        // No configured invocation tiers here: their arguments target solution-level dotnet commands,
        // and `dotnet run` would forward them to the app instead of consuming them.
        return _processRunner.RunAsync(
            DotNetMuxer.Path,
            ["run", path],
            environment: ChildEnvironment(environment),
            workingDirectory: workingDirectory,
            throwOnNonZero: throwOnNonZero,
            onStdout: x => _reporter.ChildOutput(x, null),
            onStderr: x => _reporter.ChildError(x, null),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Picks the NuGet push feed for a version from the resolved <c>nuget.feeds</c> section: the
    /// <c>prerelease</c> channel for prerelease versions, the <c>release</c> channel otherwise. The
    /// configuration factory has already folded a missing prerelease feed onto the release feed, so a
    /// <see langword="null"/> channel means no feed is configured at all.
    /// </summary>
    /// <param name="feeds">The resolved push feeds.</param>
    /// <param name="isPrerelease">Whether the version being pushed is a prerelease.</param>
    /// <returns>The feed to push to.</returns>
    /// <exception cref="BuildFailedException">No applicable feed is configured.</exception>
    internal static NuGetFeedConfig ResolvePushFeed(NuGetFeedsConfig feeds, bool isPrerelease)
    {
        Guard.IsNotNull(feeds);
        return (isPrerelease ? feeds.Prerelease : feeds.Release)
            ?? throw new BuildFailedException(
                "No NuGet feed is configured. "
                + "Set 'nuget.feeds.release' (and optionally 'nuget.feeds.prerelease') in the configuration file.");
    }

    /// <summary>
    /// Folds the base arguments, the resolved invocation configuration, and the trailing arguments into the
    /// final argument list for a <c>dotnet</c> invocation.
    /// </summary>
    /// <param name="args">The base arguments constructed by the calling method.</param>
    /// <param name="invocation">The resolved invocation configuration; its arguments (<c>dotnet.all</c>, the
    /// per-command section, and any forwarded command-line arguments, already folded in that order by the
    /// configuration factory) are appended after <paramref name="args"/>.</param>
    /// <param name="trailingArgs">Arguments appended after everything else, so that neither configured nor
    /// forwarded arguments can countermand them.</param>
    /// <returns>The final argument list.</returns>
    internal static string[] MergeInvocation(
        IReadOnlyList<string> args,
        DotNetInvocationConfig invocation,
        IReadOnlyList<string> trailingArgs)
        => [.. args, .. invocation.Args, .. trailingArgs];

    /// <summary>
    /// Computes the environment for a child process: the given variables layered over the removal of the
    /// delegation marker.
    /// </summary>
    /// <remarks>
    /// <see cref="DelegationService.DelegatedEnvVar"/> means "this process is the delegation target", which is
    /// only true for the delegated bv itself. Inherited any further, the marker would make a bv reached through
    /// a child process — a hook shelling out to a globally-installed bv, say — skip delegation and
    /// silently run in place at whatever version it happens to be. Every child bv spawns therefore gets the
    /// marker removed (a <see langword="null"/> value removes the variable), while explicitly configured
    /// values, layered on top, still win.
    /// </remarks>
    /// <param name="environment">The variables the invocation wants to set, or <see langword="null"/> for none.</param>
    /// <returns>The environment to pass to the child process.</returns>
    internal static Dictionary<string, string?> ChildEnvironment(IReadOnlyDictionary<string, string?>? environment)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal) { [DelegationService.DelegatedEnvVar] = null };
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Return a parameter string that reflects whether we're running in CI.
    /// </summary>
    /// <param name="asMSBuildPassthrough"><see langword="true"/> to use the MSBuild passthrough form (<c>-p:</c>),
    /// <see langword="false"/> to use the standard form (<c>--property:</c>).</param>
    /// <returns>A string representing the ContinuousIntegrationBuild property setting parameter for the <c>dotnet</c> command.</returns>
    private string ContinuousIntegrationBuildArg(bool asMSBuildPassthrough)
    {
        var prefix = asMSBuildPassthrough ? "-p:" : "--property:";
        return $"{prefix}ContinuousIntegrationBuild={(_server.IsCloudBuild ? "true" : "false")}";
    }

    /// <summary>
    /// Runs a <c>dotnet</c> command, forwarding the output to the reporter according to the current verbosity.
    /// </summary>
    /// <param name="args">The base arguments to pass to <c>dotnet</c>, constructed by the calling method.</param>
    /// <param name="invocation">The resolved invocation configuration: its arguments are appended after
    /// <paramref name="args"/>, and its environment variables are applied to the child process. Pass an empty
    /// instance for bv-internal invocations that must not receive user-configured or forwarded arguments
    /// (e.g. MSBuild property probes).</param>
    /// <param name="trailingArgs">Arguments appended after everything else, so they cannot be overridden by
    /// configured or forwarded arguments (e.g. the <c>ContinuousIntegrationBuild</c> property).</param>
    /// <param name="appendVerbosity"><see langword="true"/> to append a <c>--verbosity</c> argument reflecting the
    /// current verbosity; <see langword="false"/> for commands that do not accept it (e.g. <c>dotnet nuget push</c>)
    /// or already carry it.</param>
    /// <param name="outputStreaming">The output streaming configuration for the <c>dotnet</c> invocation.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task{ProcessResult}"/> representing the ongoing operation, with a result
    /// describing child process outcome.</returns>
    private Task<ProcessResult> RunDotNetAsync(
        IReadOnlyList<string> args,
        DotNetInvocationConfig invocation,
        IReadOnlyList<string> trailingArgs,
        bool appendVerbosity,
        OutputStreaming outputStreaming,
        CancellationToken cancellationToken = default)
    {
        var finalArgs = MergeInvocation(args, invocation, trailingArgs);
        return _processRunner.RunAsync(
            DotNetMuxer.Path,
            appendVerbosity ? finalArgs.Append($"--verbosity={_reporter.Verbosity}") : finalArgs,
            environment: ChildEnvironment(invocation.Env),
            onStdout: outputStreaming.Enabled ? x => _reporter.ChildOutput(x, outputStreaming.Verbosity) : null,
            onStderr: outputStreaming.Enabled ? x => _reporter.ChildError(x, outputStreaming.Verbosity) : null,
            cancellationToken: cancellationToken);
    }
}
