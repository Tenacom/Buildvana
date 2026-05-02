// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core;
using Buildvana.Tool.Infrastructure;
using Cake.Core;
using Cake.Core.IO;
using CommunityToolkit.Diagnostics;

using IProcessRunner = Buildvana.Core.Process.IProcessRunner;

namespace Buildvana.Tool.Utilities;

partial class CakeContextExtensions
{
    /// <summary>
    /// Executes an external command, capturing standard output and failing if the exit code is not zero.
    /// </summary>
    /// <param name="this">The Cake context.</param>
    /// <param name="command">The name of the command to execute.</param>
    /// <param name="arguments">The arguments to pass to <paramref name="command"/>.</param>
    /// <returns>The captured output of the command.</returns>
    /// <exception cref="BuildFailedException">The command exited with a non-zero exit code.</exception>
    public static IEnumerable<string> Exec(this ICakeContext @this, string command, ProcessArgumentBuilder arguments)
    {
        Guard.IsNotNull(arguments);

        var exitCode = Exec(@this, command, arguments, out var output);
        if (exitCode != 0)
        {
            throw new BuildFailedException(
                exitCode,
                $"Command '{command} {arguments.RenderSafe()}' exited with code {exitCode}.");
        }

        return output;
    }

    /// <summary>
    /// Executes an external command, capturing standard output.
    /// </summary>
    /// <param name="this">The Cake context.</param>
    /// <param name="command">The name of the command to execute.</param>
    /// <param name="arguments">The arguments to pass to <paramref name="command"/>.</param>
    /// <param name="output">When this method returns, the captured output of the command. This parameter is passed uninitialized.</param>
    /// <returns>The exit code of the command.</returns>
    public static int Exec(this ICakeContext @this, string command, ProcessArgumentBuilder arguments, out IEnumerable<string> output)
    {
        Guard.IsNotNull(arguments);

        // Resolve the runner via DI through the BuildContext cast: this extension exists only during the
        // Cake.Frosting transition and disappears together with ICakeContext once the migration completes.
        var runner = ((BuildContext)@this).GetService<IProcessRunner>();

        // ProcessArgumentBuilder enumerates as IProcessArgument; .Render() yields the surface form per token.
        // Pre-quoted tokens (e.g. AppendQuoted) round-trip incorrectly because CliWrap re-quotes them, but
        // no current Exec call site uses quoted tokens. Revisit if that ever changes.
        var args = arguments.Select(a => a.Render()).ToList();

        var result = runner
            .RunAsync(command, args, throwOnNonZero: false)
            .GetAwaiter()
            .GetResult();

        output = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return result.ExitCode;
    }
}
