// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Declares the <c>bv</c> command implemented by the decorated class: its name (as typed on the command line)
/// and whether it forwards all of its arguments verbatim to the underlying <c>dotnet</c> invocation(s).
/// </summary>
/// <remarks>
/// <para>This attribute is the single source of truth for the command name → implementing class association,
/// the "consume all arguments" flag, and the command's settings type. <see cref="CommandRegistry"/> discovers
/// decorated classes by reflection; <c>Program</c> dispatches to them and the help renderer reflects the
/// settings type. Command display order is a separate concern, defined by <see cref="CommandRegistry"/>.</para>
/// </remarks>
/// <param name="name">The command name as typed on the command line (e.g. <c>build</c>).</param>
/// <param name="consumesAllArguments">
/// <see langword="true"/> if the command forwards every non-global argument verbatim to its underlying
/// <c>dotnet</c> invocation(s); <see langword="false"/> if it binds a fixed option surface.
/// </param>
/// <param name="settingsType">
/// The command's <c>*Settings</c> type, whose <see cref="Buildvana.Tool.CommandLine.BvOptionAttribute"/>-decorated
/// properties the help renderer enumerates; <see langword="null"/> for commands with no options of their own.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class ImplementsCommandAttribute(string name, bool consumesAllArguments = false, Type? settingsType = null) : Attribute
{
    /// <summary>
    /// Gets the command name as typed on the command line.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets a value indicating whether the command forwards all of its arguments verbatim.
    /// </summary>
    public bool ConsumesAllArguments { get; } = consumesAllArguments;

    /// <summary>
    /// Gets the command's <c>*Settings</c> type, or <see langword="null"/> if it has no options of its own.
    /// </summary>
    public Type? SettingsType { get; } = settingsType;
}
