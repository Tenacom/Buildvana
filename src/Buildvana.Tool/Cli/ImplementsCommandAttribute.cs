// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Declares the <c>bv</c> command implemented by the decorated class: its name (as typed on the command line)
/// and whether it forwards all of its arguments verbatim to the underlying <c>dotnet</c> invocation(s).
/// </summary>
/// <remarks>
/// <para>This attribute is the single source of truth for the command name → implementing class association
/// and the "consume all arguments" flag. <see cref="CommandRegistry"/> discovers decorated classes by
/// reflection and uses them to register commands with Spectre and to answer forwarding questions in
/// <c>Program.Main</c> and <see cref="BvHelpProvider"/>. Command display/registration order is a separate
/// concern, defined by <see cref="CommandRegistry"/>.</para>
/// </remarks>
/// <param name="name">The command name as typed on the command line (e.g. <c>build</c>).</param>
/// <param name="consumesAllArguments">
/// <see langword="true"/> if the command forwards every non-global argument verbatim to its underlying
/// <c>dotnet</c> invocation(s); <see langword="false"/> if it binds a fixed option surface through Spectre.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class ImplementsCommandAttribute(string name, bool consumesAllArguments = false) : Attribute
{
    /// <summary>
    /// Gets the command name as typed on the command line.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets a value indicating whether the command forwards all of its arguments verbatim.
    /// </summary>
    public bool ConsumesAllArguments { get; } = consumesAllArguments;
}
