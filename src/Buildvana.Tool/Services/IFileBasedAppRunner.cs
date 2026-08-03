// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.Process;

namespace Buildvana.Tool.Services;

/// <summary>
/// Runs and cleans file-based apps (standalone C# files executed via <c>dotnet run</c>).
/// </summary>
internal interface IFileBasedAppRunner
{
    /// <summary>
    /// Asynchronously runs a file-based app.
    /// </summary>
    /// <param name="path">The path of the C# file to run.</param>
    /// <param name="environment">Environment variables to apply on top of the inherited environment.
    /// A <see langword="null"/> value removes the variable.</param>
    /// <param name="workingDirectory">The working directory for the app, or <see langword="null"/>
    /// to inherit the current directory.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, with a result describing
    /// the process outcome.</returns>
    /// <exception cref="BuildFailedException">The app exited with a non-zero exit code.</exception>
    Task<ProcessResult> RunFileBasedAppAsync(
        string path,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously clears the build cache of a file-based app.
    /// </summary>
    /// <param name="path">The path of the C# file whose build cache to clear.</param>
    /// <param name="cancellationToken">A token that, when signalled, terminates the spawned process.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the ongoing operation, with a result describing
    /// the process outcome.</returns>
    /// <exception cref="BuildFailedException">The clean operation exited with a non-zero exit code.</exception>
    Task<ProcessResult> CleanFileBasedAppAsync(string path, CancellationToken cancellationToken = default);
}
