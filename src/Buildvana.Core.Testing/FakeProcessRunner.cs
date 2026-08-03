// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.Process;

namespace Buildvana.Core.Testing;

/// <summary>
/// A capture-and-script fake for <see cref="IProcessRunner"/>. No process is ever spawned.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<(string Executable, IReadOnlyList<string> Args, string? WorkingDirectory)> _runs = [];

    /// <summary>
    /// Gets the invocations of <see cref="RunAsync"/>, in order.
    /// </summary>
    public IReadOnlyList<(string Executable, IReadOnlyList<string> Args, string? WorkingDirectory)> Runs => _runs;

    /// <summary>
    /// Gets or sets a callback that produces the result of each invocation, simulating the process's behavior.
    /// When <see langword="null"/>, every invocation succeeds with exit code 0 and empty output.
    /// </summary>
    public Func<string, IReadOnlyList<string>, ProcessResult>? OnRun { get; set; }

    /// <inheritdoc/>
    public Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        var argList = args.ToList();
        _runs.Add((executable, argList, workingDirectory));
        var commandLine = $"{executable} {string.Join(' ', argList)}";
        var result = OnRun?.Invoke(executable, argList) ?? new ProcessResult(commandLine, 0, string.Empty, string.Empty, TimeSpan.Zero);
        if (throwOnNonZero && result.ExitCode != 0)
        {
            throw new BuildFailedException($"Process failed with exit code {result.ExitCode}: {commandLine}");
        }

        return Task.FromResult(result);
    }
}
