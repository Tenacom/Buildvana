// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
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
    private const int TailCapBytes = 4096;

    private readonly IBuildHost _host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessRunner"/> class.
    /// </summary>
    /// <param name="host">The build host through which a non-zero exit code is reported.</param>
    public ProcessRunner(IBuildHost host)
    {
        Guard.IsNotNull(host);
        _host = host;
    }

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
            _host.Fail(result.ExitCode, BuildFailureMessage(executable, result));
        }

        return result;
    }

    private static string BuildFailureMessage(string executable, ProcessResult result)
    {
        var stdout = FormatTail(result.StandardOutput);
        var stderr = FormatTail(result.StandardError);
        var header = string.Create(
            CultureInfo.InvariantCulture,
            $"Command '{executable}' exited with code {result.ExitCode}.");
        if (stdout is null && stderr is null)
        {
            return header;
        }

        var sb = new StringBuilder(header);
        if (stdout is not null)
        {
            _ = sb.Append("\n--- stdout ---\n").Append(stdout);
        }

        if (stderr is not null)
        {
            _ = sb.Append("\n--- stderr ---\n").Append(stderr);
        }

        return sb.ToString();
    }

    private static string? FormatTail(string text)
    {
        var trimmed = text.TrimEnd();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(trimmed);
        if (bytes.Length <= TailCapBytes)
        {
            return trimmed;
        }

        var start = bytes.Length - TailCapBytes;
        while (start < bytes.Length && (bytes[start] & 0xC0) == 0x80)
        {
            start++;
        }

        return "…" + Encoding.UTF8.GetString(bytes, start, bytes.Length - start).TrimStart();
    }
}
