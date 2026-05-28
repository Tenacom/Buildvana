// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core.Process.Internal;
using CliWrap;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Process;

/// <summary>
/// CliWrap-backed implementation of <see cref="IProcessRunner"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private const int OutputHeadLines = 20;
    private const int OutputTailLines = 20;

    /// <inheritdoc cref="IProcessRunner.RunAsync"/>
    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(executable);

        // ReSharper disable once PossibleMultipleEnumeration
        Guard.IsNotNull(args);

        var stdoutBuffer = new StringBuilder();
        var stderrBuffer = new StringBuilder();
        var stdoutCapture = new HeadTailPipeTarget(maxHeadLines: OutputHeadLines, maxTailLines: OutputTailLines);
        var stderrCapture = new HeadTailPipeTarget(maxHeadLines: OutputHeadLines, maxTailLines: OutputTailLines);

        // When the caller wants line-by-line stdout, fan the stream out to their callback too.
        var stdoutPipe = onStdout is null
            ? PipeTarget.Merge(
                stdoutCapture,
                PipeTarget.ToStringBuilder(stdoutBuffer))
            : PipeTarget.Merge(
                stdoutCapture,
                PipeTarget.ToStringBuilder(stdoutBuffer),
                PipeTarget.ToDelegate(onStdout));

        // When the caller wants line-by-line stderr, fan the stream out to their callback too.
        var stderrPipe = onStderr is null
            ? PipeTarget.Merge(
                stderrCapture,
                PipeTarget.ToStringBuilder(stderrBuffer))
            : PipeTarget.Merge(
                stderrCapture,
                PipeTarget.ToStringBuilder(stderrBuffer),
                PipeTarget.ToDelegate(onStderr));

        var command = Cli.Wrap(executable)

            // ReSharper disable once PossibleMultipleEnumeration
            .WithArguments(args)
            .WithStandardOutputPipe(stdoutPipe)
            .WithStandardErrorPipe(stderrPipe)
            .WithValidation(CommandResultValidation.None);

        if (workingDirectory is not null)
        {
            command = command.WithWorkingDirectory(workingDirectory);
        }

        var commandResult = await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        var result = new ProcessResult(
            command.ToString(),
            commandResult.ExitCode,
            stdoutBuffer.ToString(),
            stderrBuffer.ToString(),
            commandResult.RunTime);

        if (throwOnNonZero && result.ExitCode != 0)
        {
            throw new BuildFailedException(result.ExitCode, BuildFailureMessage(executable, result, stdoutCapture, stderrCapture));
        }

        return result;
    }

    private static string BuildFailureMessage(string executable, ProcessResult result, HeadTailPipeTarget stdoutCapture, HeadTailPipeTarget stderrCapture)
        => new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Command '{executable}' exited with code {result.ExitCode}.")
            .AppendHeader("full command line")
            .AppendLine(result.CommandLine)
            .AppendHeadTail(stdoutCapture, "stdout")
            .AppendHeadTail(stderrCapture, "stderr")
            .ToString();
}
