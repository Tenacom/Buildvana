// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        IReadOnlyDictionary<string, string?>? environment = null,
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

        if (environment is not null)
        {
            command = command.WithEnvironmentVariables(environment);
        }

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

    /// <inheritdoc cref="IProcessRunner.RunWithInheritedStdioAsync"/>
    public async Task<int> RunWithInheritedStdioAsync(
        string executable,
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrEmpty(executable);

        // ReSharper disable once PossibleMultipleEnumeration
        Guard.IsNotNull(args);

        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };

        // ReSharper disable once PossibleMultipleEnumeration
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                if (value is null)
                {
                    _ = startInfo.Environment.Remove(name);
                }
                else
                {
                    startInfo.Environment[name] = value;
                }
            }
        }

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        // Keep this process alive across Ctrl-C: the child shares the console and receives the same signal;
        // it owns its shutdown, and this process must survive to observe and report the exit code.
        static void SuppressCancel(object? sender, ConsoleCancelEventArgs e) => e.Cancel = true;
        Console.CancelKeyPress += SuppressCancel;
        try
        {
            using var process = StartProcess(startInfo);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(process);
                throw;
            }

            return process.ExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= SuppressCancel;
        }
    }

    private static System.Diagnostics.Process StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return System.Diagnostics.Process.Start(startInfo)
                ?? throw new BuildFailedException($"Failed to start '{startInfo.FileName}'.");
        }
        catch (Win32Exception e)
        {
            throw new BuildFailedException($"Failed to start '{startInfo.FileName}': {e.Message}", e);
        }
    }

    private static void KillProcessTree(System.Diagnostics.Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the cancellation and the kill: there is nothing left to stop.
        }
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
