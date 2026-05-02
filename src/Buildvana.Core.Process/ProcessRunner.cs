// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Process;

/// <summary>
/// CliWrap-backed implementation of <see cref="IProcessRunner"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc cref="IProcessRunner.RunAsync"/>
    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        Action<string>? onStdout = null,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(executable);
        Guard.IsNotNull(args);

        var stdoutBuffer = new StringBuilder();
        var stderrBuffer = new StringBuilder();

        // When the caller wants line-by-line stdout, fan the stream out to both the buffer and their callback.
        var stdoutPipe = onStdout is null
            ? PipeTarget.ToStringBuilder(stdoutBuffer)
            : PipeTarget.Merge(
                PipeTarget.ToStringBuilder(stdoutBuffer),
                PipeTarget.ToDelegate(onStdout));

        var command = Cli.Wrap(executable)
            .WithArguments(args)
            .WithStandardOutputPipe(stdoutPipe)
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderrBuffer))
            .WithValidation(CommandResultValidation.None);

        if (workingDirectory is not null)
        {
            command = command.WithWorkingDirectory(workingDirectory);
        }

        var commandResult = await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        var result = new ProcessResult(
            commandResult.ExitCode,
            stdoutBuffer.ToString(),
            stderrBuffer.ToString(),
            commandResult.RunTime);

        if (throwOnNonZero && result.ExitCode != 0)
        {
            throw new BuildFailedException(
                result.ExitCode,
                $"Command '{executable}' exited with code {result.ExitCode}.");
        }

        return result;
    }
}
