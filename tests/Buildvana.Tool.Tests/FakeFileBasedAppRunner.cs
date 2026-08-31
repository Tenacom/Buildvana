// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
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
    /// Gets or sets a callback invoked during <see cref="RunFileBasedAppAsync"/>, simulating the app's behavior.
    /// Exceptions thrown by the callback propagate to the caller, simulating a failing app.
    /// </summary>
    public Action<string, IReadOnlyDictionary<string, string?>?, string?>? OnRun { get; set; }

    /// <summary>
    /// Gets or sets the exit code the app is to answer with. The real runner throws on a non-zero exit code
    /// unless the caller says otherwise, and so does this one.
    /// </summary>
    public int ExitCode { get; set; }

    /// <inheritdoc/>
    public Task<ProcessResult> RunFileBasedAppAsync(
        string path,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        CancellationToken cancellationToken = default)
    {
        Runs.Add((path, environment, workingDirectory));
        OnRun?.Invoke(path, environment, workingDirectory);
        var commandLine = $"dotnet run {path}";
        if (throwOnNonZero && ExitCode != 0)
        {
            throw new BuildFailedException(ExitCodes.ExternalProgramFailed, $"Process failed with exit code {ExitCode}: {commandLine}");
        }

        return Task.FromResult(new ProcessResult(commandLine, ExitCode, string.Empty, string.Empty, TimeSpan.Zero));
    }
}
