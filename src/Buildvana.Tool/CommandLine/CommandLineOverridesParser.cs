// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.Configuration;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// Parses the configuration overrides out of a run's command-line parameters: the one mapping from
/// command-line flags to <see cref="CommandLineOverrides"/>.
/// </summary>
/// <remarks>
/// <para>Reading is generic rather than per-command: by the time overrides are composed,
/// <c>CommandArgumentValidator</c> has already rejected any of these flags on a command that does not
/// declare them, so a token can only be present when the executed command accepts it.</para>
/// </remarks>
internal static class CommandLineOverridesParser
{
    /// <summary>
    /// Parses the configuration overrides from a command's option tokens.
    /// </summary>
    /// <param name="parameters">The command-line parameters of the run.</param>
    /// <returns>The parsed overrides.</returns>
    /// <exception cref="BuildFailedException">An option value is invalid or missing.</exception>
    public static CommandLineOverrides Parse(CommandParameters parameters)
    {
        Guard.IsNotNull(parameters);
        var reader = new CliOptionReader(parameters.Options);

        // A configuration stated among the forwarded arguments decides the actual build, so bv's own view
        // must agree with it: `bv pack -- -c Debug` resolves bv's configuration to Debug. bv owns these two
        // option names wherever they appear in the forwarded stream: reading consumes the tokens, and only
        // the stripped remainder reaches `dotnet`. Left in, they would fail every pipeline run at the
        // Restore step (`dotnet restore` rejects `-c`); bv instead injects the resolved configuration
        // itself, in the form each command accepts. The read happens unconditionally, before the overrides
        // compose, so the stream is stripped even when bv's own option surface supplies the configuration.
        var forwardedReader = new CliOptionReader(parameters.Forwarded);
        var forwardedConfiguration = forwardedReader.ReadValue("--configuration", "-c");
        return new CommandLineOverrides
        {
            Configuration = reader.ReadValue("--configuration", "-c") ?? forwardedConfiguration,
            CheckPublicApi = reader.ReadBoolValue("--check-public-api"),
            Dogfood = reader.ReadBoolValue("--dogfood"),
            ForwardedArgs = forwardedReader.Remaining is { Count: > 0 } remaining ? remaining : null,
        };
    }
}
