// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core.Process;
using Buildvana.Tool.Services;

/// <summary>
/// A capture-and-script fake for <see cref="IFileBasedAppRunner"/>.
/// </summary>
internal sealed class FakeFileBasedAppRunner : IFileBasedAppRunner
{
    /// <summary>
    /// Gets the invocations of <see cref="RunFileBasedAppAsync"/>, in order.
    /// </summary>
    public List<(string Path, IReadOnlyDictionary<string, string?>? Environment, string? WorkingDirectory)> Runs { get; } = [];

    /// <summary>
    /// Gets the paths passed to <see cref="CleanFileBasedAppAsync"/>, in order.
    /// </summary>
    public List<string> CleanedPaths { get; } = [];

    /// <summary>
    /// Gets or sets a callback invoked during <see cref="RunFileBasedAppAsync"/>, simulating the app's behavior.
    /// Exceptions thrown by the callback propagate to the caller, simulating a failing app.
    /// </summary>
    public Action<string, IReadOnlyDictionary<string, string?>?, string?>? OnRun { get; set; }

    /// <inheritdoc/>
    public Task<ProcessResult> RunFileBasedAppAsync(
        string path,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        Runs.Add((path, environment, workingDirectory));
        OnRun?.Invoke(path, environment, workingDirectory);
        return Task.FromResult(Result($"dotnet run {path}"));
    }

    /// <inheritdoc/>
    public Task<ProcessResult> CleanFileBasedAppAsync(string path, CancellationToken cancellationToken = default)
    {
        CleanedPaths.Add(path);
        return Task.FromResult(Result($"dotnet clean {path}"));
    }

    private static ProcessResult Result(string commandLine)
        => new(commandLine, 0, string.Empty, string.Empty, TimeSpan.Zero);
}
