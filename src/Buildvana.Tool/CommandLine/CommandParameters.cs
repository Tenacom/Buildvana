// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// DI singleton holding the non-global command-line tokens for the dispatched command, as split by
/// <see cref="CliArgSplitter"/>: the options it should parse, its positional parameters, and the tokens
/// forwarded verbatim after the <c>--</c> separator.
/// </summary>
internal sealed record CommandParameters(

    /// <summary>
    /// Option tokens before <c>--</c> (minus globals, subcommand, and positionals), for the
    /// command's settings to parse. Empty when none were given.
    /// </summary>
    IReadOnlyList<string> Options,

    /// <summary>
    /// Tokens after the first <c>--</c>, forwarded verbatim to the invoked external command,
    /// when applicable. Empty when none.
    /// </summary>
    IReadOnlyList<string> Forwarded);
