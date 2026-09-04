// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// DI singleton holding the non-global command-line tokens for the dispatched command: the options it should
/// parse, its positional parameters, and the tokens given after the <c>--</c> separator. <see cref="CliArgSplitter"/>
/// splits the command line, and <see cref="Buildvana.Tool.Infrastructure.Execution.CommandArgumentValidator"/>
/// tells the command's options from its positionals, which takes the command's own declarations.
/// </summary>
/// <param name="Options">The non-global, non-positional tokens before <c>--</c>, for the command to parse. Empty when none were given.</param>
/// <param name="Positionals">The positional tokens for the command to bind: those left over after subcommand
/// resolution, plus every operand written after an option. Empty when none were given.</param>
/// <param name="Forwarded">The tokens after the first <c>--</c>, as typed; the overrides parser consumes the
/// ones bv owns and forwards the rest to the invoked external command. Empty when none.</param>
internal sealed record CommandParameters(
    IReadOnlyList<string> Options,
    IReadOnlyList<string> Positionals,

    // ReSharper disable once NotAccessedPositionalProperty.Global -- false positive: Forwarded is read by CommandLineOverridesParser
    IReadOnlyList<string> Forwarded);
