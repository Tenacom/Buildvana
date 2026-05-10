// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Cli;

/// <summary>
/// Bv-global options pre-parsed from the command-line args in <c>Program.Main</c> before Spectre dispatches the subcommand.
/// </summary>
/// <param name="Verbosity">The raw <c>--verbosity</c> / <c>-v</c> value, or <see langword="null"/> if none was passed.</param>
/// <param name="Color">Whether <c>--color</c> was passed.</param>
/// <param name="NoColor">Whether <c>--no-color</c> was passed.</param>
/// <param name="Nologo">Whether <c>--nologo</c> was passed.</param>
/// <param name="Version">Whether <c>--version</c> was passed.</param>
internal sealed record GlobalOptions(string? Verbosity, bool Color, bool NoColor, bool Nologo, bool Version);
