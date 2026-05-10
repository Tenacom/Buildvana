// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Global options shared by every Spectre command.
/// </summary>
/// <remarks>The options declared here are parsed in <c>Program.Main</c> by a pre-scan that strips
/// them from the args before Spectre sees them. The properties are therefore always at their defaults
/// at parse time; they exist so the options appear in the global-options section of help and can be
/// passed at any position on the command line (before or after the subcommand name).</remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class BaseSettings : CommandSettings
{
    /// <summary>
    /// Gets the requested logging verbosity.
    /// </summary>
    [CommandOption("-v|--verbosity <LEVEL>")]
    [Description("Logging verbosity. One of: quiet, minimal, normal, detailed, diagnostic. Defaults to normal.")]
    public string? Verbosity { get; init; }

    /// <summary>
    /// Gets a value indicating whether ANSI color output is forced even when not connected to a TTY.
    /// </summary>
    [CommandOption("--color")]
    [Description("Force ANSI color output even when not connected to a TTY.")]
    public bool Color { get; init; }

    /// <summary>
    /// Gets a value indicating whether ANSI color output is disabled.
    /// </summary>
    [CommandOption("--no-color")]
    [Description("Disable ANSI color output.")]
    public bool NoColor { get; init; }

    /// <summary>
    /// Gets a value indicating whether the startup logo line is suppressed.
    /// </summary>
    [CommandOption("--nologo")]
    [Description("Suppress the startup logo line.")]
    public bool Nologo { get; init; }

    /// <summary>
    /// Gets a value indicating whether the version is requested.
    /// </summary>
    [CommandOption("--version")]
    [Description("Print the bv version and exit.")]
    public bool Version { get; init; }
}
