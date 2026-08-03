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
using Buildvana.Core.HomeDirectory;
using Buildvana.Tool.Utilities;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Hooks;

/// <summary>
/// Runs repo-owned hooks: optional file-based apps at well-known paths of the form
/// <c>.buildvana/hooks/{command}/{moment}.cs</c>, executed at named moments of bv commands.
/// </summary>
internal sealed class HookRunner
{
    /// <summary>
    /// The name of the environment variable through which the absolute path of the serialized
    /// context file is published to hooks.
    /// </summary>
    public const string ContextEnvironmentVariable = "BV_HOOK_CONTEXT";

    private static readonly JsonSerializerOptions ContextSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

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
    /// Runs the hook for the given command and moment, if its file exists.
    /// </summary>
    /// <param name="command">The name of the command the hook belongs to.</param>
    /// <param name="moment">The name of the moment the hook fires at.</param>
    /// <param name="context">The context to serialize and hand to the hook through the
    /// <see cref="ContextEnvironmentVariable"/> environment variable.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the hook process.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, whose result is
    /// <see langword="true"/> if the hook ran and completed successfully, or <see langword="false"/>
    /// if there is no hook file.</returns>
    /// <exception cref="BuildFailedException">The hook exited with a non-zero exit code.</exception>
    public async Task<bool> RunHookAsync(string command, string moment, object context, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(command);
        Guard.IsNotNullOrEmpty(moment);
        Guard.IsNotNull(context);
        var hookName = $"{command}/{moment}";
        var relativePath = Path.Combine(".buildvana", "hooks", command, moment + ".cs");
        var path = Path.GetFullPath(relativePath, _home.HomeDirectory);
        if (!File.Exists(path))
        {
            _reporter.Info($"Hook {hookName}: skipped: no {relativePath} file.");
            return false;
        }

        var json = JsonSerializer.Serialize(context, ContextSerializerOptions);
        _reporter.Detail($"Hook {hookName}: context: {json}");
        var contextPath = Path.Combine(Path.GetTempPath(), $"bv-hook-context-{Path.GetRandomFileName()}.json");
        await File.WriteAllTextAsync(contextPath, json, cancellationToken).ConfigureAwait(false);
        try
        {
            _reporter.Info($"Hook {hookName}: running {relativePath}...");
            _ = await _appRunner.RunFileBasedAppAsync(
                path,
                environment: new Dictionary<string, string?>(StringComparer.Ordinal) { [ContextEnvironmentVariable] = contextPath },
                workingDirectory: _home.HomeDirectory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(contextPath);
        }

        return true;
    }

    /// <summary>
    /// Clears the build cache of every hook file under <c>.buildvana/hooks/</c>.
    /// </summary>
    /// <param name="cancellationToken">A token that, when signalled, terminates the ongoing clean operation.</param>
    /// <returns>A <see cref="Task"/> representing the ongoing operation.</returns>
    public async Task CleanBuildCachesAsync(CancellationToken cancellationToken = default)
    {
        var hooksRelativePath = Path.Combine(".buildvana", "hooks");
        var hooksPath = Path.GetFullPath(hooksRelativePath, _home.HomeDirectory);
        if (!FileSystemHelper.DirectoryExists(hooksPath))
        {
            _reporter.Detail($"Hook build cache cleaning skipped: no {hooksRelativePath} directory.");
            return;
        }

        foreach (var path in FileSystemHelper.EnumerateFiles(hooksPath, "**/*.cs"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _reporter.Info($"Clearing build cache of hook file {path}...");
            _ = await _appRunner.CleanFileBasedAppAsync(path, cancellationToken).ConfigureAwait(false);
        }
    }
}
