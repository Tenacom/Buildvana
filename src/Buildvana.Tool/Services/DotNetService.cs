// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Process;
using Buildvana.Tool.Configuration;
using Buildvana.Tool.Infrastructure;
using Buildvana.Tool.Services.ServerAdapters;
using Buildvana.Tool.Services.Solution;
using Buildvana.Tool.Services.Versioning;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services;

/// <summary>
/// Provides shortcut methods for .NET SDK operations.
/// </summary>
internal sealed partial class DotNetService
{
    // The muxer sets DOTNET_HOST_PATH to the full path of the dotnet executable that launched us,
    // so we re-invoke that exact host instead of relying on `dotnet` being on PATH.
    private static readonly string DotNetMuxer
        = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } p
            ? p
            : "dotnet";

    private readonly IReporter _reporter;
    private readonly IProcessRunner _processRunner;
    private readonly Lazy<NuGetPushConfiguration> _nugetPushConfigurationLazy;
    private readonly ServerAdapter _server;
    private readonly VersionService _version;
    private readonly DotNetSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotNetService"/> class.
    /// </summary>
    public DotNetService(
        IReporter reporter,
        IProcessRunner processRunner,
        Lazy<NuGetPushConfiguration> nugetPushConfigurationLazy,
        ServerAdapter server,
        VersionService version,
        DotNetSettings settings)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(processRunner);
        Guard.IsNotNull(nugetPushConfigurationLazy);
        Guard.IsNotNull(server);
        Guard.IsNotNull(version);
        Guard.IsNotNull(settings);
        _reporter = reporter;
        _processRunner = processRunner;
        _nugetPushConfigurationLazy = nugetPushConfigurationLazy;
        _server = server;
        _version = version;
        _settings = settings;
    }

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
        string[] args = [
            "restore",
            solution.SolutionPath,
            "--disable-parallel",
            "-nologo",
        ];

        return RunDotNetAsync(
            args,
            tiers: [_settings.All, _settings.Restore],
            commandLineArgs: forwardedArgs,
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
        string[] args = [
            "build",
            solution.SolutionPath,
            "-nologo",
            $"-p:Configuration={configuration}",
            .. restore ? Array.Empty<string>() : ["--no-restore"],
        ];

        return RunDotNetAsync(
            args,
            tiers: [_settings.All, _settings.Build],
            commandLineArgs: forwardedArgs,
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
            string[] probeArgs = ["msbuild", projectPath, "-nologo", "-getProperty:IsTestingPlatformApplication"];
            var probe = await RunDotNetAsync(
                probeArgs,
                tiers: [],
                commandLineArgs: [],
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
            _reporter.Info("No test projects found, skipping tests.");
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
            tiers: [_settings.All, _settings.Test],
            commandLineArgs: forwardedArgs,
            trailingArgs: [ContinuousIntegrationBuildArg(asMSBuildPassthrough: false)],
            appendVerbosity: true,
            outputStreaming: OutputStreaming.Unconditional,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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
            tiers: [_settings.All, _settings.Pack],
            commandLineArgs: forwardedArgs,
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
        var packages = FileSystemHelper.EnumerateFiles(artifactsPath, "*.nupkg").ToArray();
        if (packages.Length == 0)
        {
            _reporter.Detail("No .nupkg files to push.");
            return;
        }

        var isPrivate = await _server.IsPrivateRepositoryAsync().ConfigureAwait(false);
        var nugetConfig = _nugetPushConfigurationLazy.Value;
        var target = isPrivate ? nugetConfig.Private
            : _version.IsPrerelease ? nugetConfig.Prerelease
            : nugetConfig.Release;
        foreach (var path in packages)
        {
            _reporter.Detail($"Pushing {path} to {target.Source}...");
            string[] args = [

                // `dotnet nuget` has no verbosity option; use the global diagnostics flag when diagnostic output is enabled.
                .. _reporter.IsVerbosityAtLeast(Verbosity.Diagnostic) ? ["-d"] : Array.Empty<string>(),
                "nuget",
                "push",
                path,
                "--source",
                target.Source,
                "--api-key",
                target.ApiKey,
                "--skip-duplicate",
            ];
            await RunDotNetAsync(
                args,
                tiers: [_settings.All, _settings.NugetPush],
                commandLineArgs: [],
                trailingArgs: [],
                appendVerbosity: false,
                outputStreaming: OutputStreaming.AtVerbosity(Verbosity.Normal),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        _reporter.Info($"Pushed {packages.Length} packages to {target.Source}.");
    }

    /// <summary>
    /// Folds the base arguments, configured tiers, command-line arguments, and trailing arguments into the final
    /// argument list and resolved environment for a <c>dotnet</c> invocation.
    /// </summary>
    /// <param name="args">The base arguments constructed by the calling method.</param>
    /// <param name="tiers">The configured settings to apply, folded left to right: each tier's arguments are appended
    /// (after <paramref name="args"/>) and its environment variables are layered on top of the previous tiers', so a
    /// later tier overrides an earlier one with the same variable name.</param>
    /// <param name="commandLineArgs">Extra arguments forwarded verbatim from the command line, appended after the
    /// configured arguments so they take precedence over them.</param>
    /// <param name="trailingArgs">Arguments appended after everything else, so they cannot be overridden by configured
    /// or command-line arguments.</param>
    /// <returns>A tuple containing the final argument list and the resolved environment, the latter being
    /// <see langword="null"/> when no tier contributes an environment variable (which leaves the child environment
    /// unchanged).</returns>
    internal static (string[] Args, IReadOnlyDictionary<string, string?>? Environment) MergeInvocation(
        IReadOnlyList<string> args,
        IReadOnlyList<DotNetInvocationSettings> tiers,
        IReadOnlyList<string> commandLineArgs,
        IReadOnlyList<string> trailingArgs)
    {
        // Fold the tiers in a single pass: append each tier's arguments, layer its environment variables on top of
        // the previous tiers' (so a later tier overrides an earlier one with the same name). A null result environment
        // leaves the child environment unchanged.
        var foldedArgs = new List<string>(args);
        Dictionary<string, string?>? environment = null;
        foreach (var tier in tiers)
        {
            foldedArgs.AddRange(tier.Args);
            foreach (var (key, value) in tier.Env)
            {
                environment ??= new Dictionary<string, string?>(StringComparer.Ordinal);
                environment[key] = value;
            }
        }

        // Order: base args → tier args → command-line args (override configured args) → trailing args (override
        // everything, so a forwarded argument cannot countermand them).
        string[] finalArgs = [.. foldedArgs, .. commandLineArgs, .. trailingArgs];
        return (finalArgs, environment);
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
    /// <param name="tiers">The configured settings to apply, folded left to right: each tier's arguments are
    /// appended (after <paramref name="args"/>) and its environment variables are layered on top of the previous
    /// tiers', so a later tier overrides an earlier one with the same variable name. Pass an empty list for
    /// bv-internal invocations that must not receive user-configured arguments (e.g. MSBuild property probes).</param>
    /// <param name="commandLineArgs">Extra arguments forwarded verbatim from the command line, appended after the
    /// configured arguments so they take precedence over them.</param>
    /// <param name="trailingArgs">Arguments appended after everything else, so they cannot be overridden by
    /// configured or command-line arguments (e.g. the <c>ContinuousIntegrationBuild</c> property).</param>
    /// <param name="appendVerbosity"><see langword="true"/> to append a <c>--verbosity</c> argument reflecting the
    /// current verbosity; <see langword="false"/> for commands that do not accept it (e.g. <c>dotnet nuget push</c>)
    /// or already carry it.</param>
    /// <param name="outputStreaming">The output streaming configuration for the <c>dotnet</c> invocation.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned <c>dotnet</c> child process.</param>
    /// <returns>A <see cref="Task{ProcessResult}"/> representing the ongoing operation, with a result describing child process outcome.</returns>
    private Task<ProcessResult> RunDotNetAsync(
        IReadOnlyList<string> args,
        IReadOnlyList<DotNetInvocationSettings> tiers,
        IReadOnlyList<string> commandLineArgs,
        IReadOnlyList<string> trailingArgs,
        bool appendVerbosity,
        OutputStreaming outputStreaming,
        CancellationToken cancellationToken = default)
    {
        var (finalArgs, environment) = MergeInvocation(args, tiers, commandLineArgs, trailingArgs);
        return _processRunner.RunAsync(
            DotNetMuxer,
            appendVerbosity ? finalArgs.Append($"--verbosity={_reporter.Verbosity}") : finalArgs,
            environment: environment,
            onStdout: outputStreaming.Enabled ? (x) => _reporter.ChildOutput(x, outputStreaming.Verbosity) : null,
            onStderr: outputStreaming.Enabled ? (x) => _reporter.ChildError(x, outputStreaming.Verbosity) : null,
            cancellationToken: cancellationToken);
    }
}
