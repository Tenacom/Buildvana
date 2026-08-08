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
    /// <param name="cancellationToken">A token that, when signalled, terminates the hook process.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// <see langword="true"/> if the hook ran and completed successfully, or <see langword="false"/>
    /// if there is no hook file.</returns>
    /// <exception cref="BuildFailedException">The hook exited with a non-zero exit code.</exception>
    public Task<bool> RunHookAsync<TArgs>(TArgs args, CancellationToken cancellationToken = default)
        where TArgs : HookArgs, IHookEvent
    {
        Guard.IsNotNull(args);
        return RunHookAsync(TArgs.Context, TArgs.Event, args, cancellationToken);
    }

    /// <summary>
    /// Clears the build cache of every hook file under <see cref="WellKnownPaths.HooksDirectory"/>,
    /// by deleting each file's artifacts directory (see <see cref="FileBasedAppHelper.GetArtifactsDirectory"/>).
    /// </summary>
    /// <param name="cancellationToken">A token that, when signalled, stops the operation before the next deletion.</param>
    public void CleanBuildCaches(CancellationToken cancellationToken = default)
    {
        var hooksPath = Path.GetFullPath(WellKnownPaths.HooksDirectory, _home.HomeDirectory);
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
    private async Task<bool> RunHookAsync(string context, string @event, object args, CancellationToken cancellationToken)
    {
        var hookName = $"{context}/{@event}";
        var relativePath = WellKnownPaths.GetHookFile(context, @event);
        var path = Path.GetFullPath(relativePath, _home.HomeDirectory);
        if (!File.Exists(path))
        {
            _reporter.Info($"Hook {hookName}: skipped: no {relativePath} file.");
            return false;
        }

        var json = JsonSerializer.Serialize(args, args.GetType(), BuildvanaJsonContext.Default);
        _reporter.Detail($"Hook {hookName}: args: {json}");
        var argsPath = Path.GetFullPath(WellKnownPaths.GetHookArgsFile(context, @event), _home.HomeDirectory);
        _ = UserDirectory.CreateDirectory(Path.GetDirectoryName(argsPath)!);
        await UserFile.WriteAllTextAsync(argsPath, json, cancellationToken).ConfigureAwait(false);
        _reporter.Info($"Hook {hookName}: running {relativePath}...");
        _ = await _appRunner.RunFileBasedAppAsync(
            path,
            workingDirectory: _home.HomeDirectory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }
}
