// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Global options shared by every Spectre command.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public partial class BaseSettings : CommandSettings
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
}
