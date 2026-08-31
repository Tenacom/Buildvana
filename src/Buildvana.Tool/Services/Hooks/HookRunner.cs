// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.HomeDirectory;
using Buildvana.Core.IO;
using Buildvana.Runtime;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hooks;

/// <summary>
/// Runs repo-owned hooks: optional file-based apps at well-known paths of the form
/// <c>.buildvana/hooks/{context}/{event}.cs</c> (<see cref="WellKnownPaths.GetHookFile"/>),
/// executed at named events of bv commands.
/// </summary>
internal sealed class HookRunner
{
    // The one non-zero exit code a hook may state as a result rather than as a failure, and only in a check
    // run: it says that the hook would change something.
    private const int PendingWorkExitCode = 1;

    private readonly IReporter _reporter;
    private readonly IHomeDirectoryProvider _home;
    private readonly IFileBasedAppRunner _appRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="HookRunner"/> class.
    /// </summary>
    public HookRunner(IReporter reporter, IHomeDirectoryProvider home, IFileBasedAppRunner appRunner)
    {
        Guard.IsNotNull(reporter);
        Guard.IsNotNull(home);
        Guard.IsNotNull(appRunner);
        _reporter = reporter;
        _home = home;
        _appRunner = appRunner;
    }

    /// <summary>
    /// Runs the hook identified by the type of its args, if the hook's file exists.
    /// </summary>
    /// <typeparam name="TArgs">The type of the hook's args, identifying the hook to run
    /// (see <see cref="IHookEvent"/>).</typeparam>
    /// <param name="args">The args to serialize into the hook's args file
    /// (<see cref="WellKnownPaths.GetHookArgsFile"/>) before running the hook. Its type must be
    /// registered in <see cref="BuildvanaJsonContext"/>. The file is left in place after the run,
    /// so the hook can be re-run by hand against the same args.</param>
    /// <param name="acceptsPendingWork">If <see langword="true"/>, an exit code of 1 is a result rather than
    /// a failure: the hook reports that it would change something, and the caller folds that into its own
    /// verdict. Only a check run passes this.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the hook process.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result says what came
    /// of the hook.</returns>
    /// <exception cref="BuildFailedException">The hook exited with an exit code that is a failure.</exception>
    public Task<HookOutcome> RunHookAsync<TArgs>(
        TArgs args,
        bool acceptsPendingWork = false,
        CancellationToken cancellationToken = default)
        where TArgs : HookArgs, IHookEvent
    {
        Guard.IsNotNull(args);
        return RunHookAsync(TArgs.Context, TArgs.Event, args, acceptsPendingWork, cancellationToken);
    }

    /// <summary>
    /// Clears the build cache of every hook file under <see cref="WellKnownPaths.HooksDirectory"/>,
    /// by deleting each file's artifacts directory (see <see cref="FileBasedAppHelper.GetArtifactsDirectory"/>).
    /// </summary>
    /// <param name="cancellationToken">A token that, when signalled, stops the operation before the next deletion.</param>
    public void CleanBuildCaches(CancellationToken cancellationToken = default)
    {
        var hooksPath = _home.GetFullPath(WellKnownPaths.HooksDirectory);
        if (!UserDirectory.Exists(hooksPath))
        {
            _reporter.Detail($"Hook build cache cleaning skipped: no {WellKnownPaths.HooksDirectory} directory.");
            return;
        }

        foreach (var path in UserDirectory.EnumerateFiles(hooksPath, "**/*.cs"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var artifactsDirectory = FileBasedAppHelper.GetArtifactsDirectory(path);
            if (artifactsDirectory is null)
            {
                // The cache root is per-machine, not per-hook: when it is missing, no hook has ever been built.
                _reporter.Info("Hook build cache cleaning skipped: this machine has no file-based app cache root.");
                return;
            }

            _reporter.Info($"Clearing build cache of hook file {path}...");
            UserDirectory.DeleteIfExists(artifactsDirectory, _reporter);
        }
    }

    // The core of RunHookAsync<TArgs>, working on the hook's context and event names.
    private async Task<HookOutcome> RunHookAsync(
        string context,
        string @event,
        object args,
        bool acceptsPendingWork,
        CancellationToken cancellationToken)
    {
        var hookName = $"{context}/{@event}";
        var relativePath = WellKnownPaths.GetHookFile(context, @event);
        var path = _home.GetFullPath(relativePath);
        if (!File.Exists(path))
        {
            _reporter.Info($"Hook {hookName}: skipped: no {relativePath} file.");
            return HookOutcome.NoHook;
        }

        var json = JsonSerializer.Serialize(args, args.GetType(), BuildvanaJsonContext.Default);
        _reporter.Trace($"Hook {hookName}: args: {json}");
        var argsPath = _home.GetFullPath(WellKnownPaths.GetHookArgsFile(context, @event));
        _ = UserDirectory.CreateDirectory(Path.GetDirectoryName(argsPath)!);
        await UserFile.WriteAllTextAsync(argsPath, json, cancellationToken).ConfigureAwait(false);
        _reporter.Info($"Running hook {hookName}...");
        var result = await _appRunner.RunFileBasedAppAsync(
            path,
            workingDirectory: _home.HomeDirectory,
            throwOnNonZero: !acceptsPendingWork,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // With pending work accepted, only 0 and 1 have a meaning the caller gave them; anything else is the
        // failure the runner would have reported on its own.
        if (acceptsPendingWork && result.ExitCode is not (0 or PendingWorkExitCode))
        {
            throw new BuildFailedException(
                ExitCodes.ExternalProgramFailed,
                $"Hook {hookName} failed with exit code {result.ExitCode}.");
        }

        _reporter.Notice($"Hook {hookName} ran.");
        return result.ExitCode == PendingWorkExitCode ? HookOutcome.PendingWork : HookOutcome.Completed;
    }
}
