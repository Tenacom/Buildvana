// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Buildvana.Core.ConsoleOutput;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Declares the <c>bv</c> command implemented by the decorated class: the path(s) it is invoked under
/// and whether it forwards all of its arguments verbatim to the underlying <c>dotnet</c> invocation(s).
/// </summary>
/// <remarks>
/// <para>This attribute is the single source of truth for the command path → implementing class association,
/// the "consume all arguments" flag, and the command's settings type. <see cref="CommandRegistry"/> discovers
/// decorated classes by reflection and arranges their paths in a tree; <c>Program</c> dispatches to them and the
/// help renderer reflects the settings type. Command display order is a separate concern, defined by
/// <see cref="CommandRegistry"/>.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class ImplementsCommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImplementsCommandAttribute"/> class from an alias list.
    /// </summary>
    /// <param name="aliases">
    /// One or more <c>'|'</c>-separated aliases, each a space-separated path as typed on the command line
    /// (e.g. <c>"build"</c>, <c>"version advance"</c>, <c>"version show | version"</c>). Excess whitespace is
    /// ignored and segments are normalized to lowercase. The first alias is the canonical path, used in help
    /// and error messages; the others are dispatch-only shortcuts.
    /// </param>
    /// <param name="consumesAllArguments">
    /// <see langword="true"/> if the command forwards every non-global argument verbatim to its underlying
    /// <c>dotnet</c> invocation(s); <see langword="false"/> if it binds a fixed option surface.
    /// </param>
    /// <param name="settingsType">
    /// The command's <c>*Settings</c> type, whose <see cref="Buildvana.Tool.CommandLine.BvOptionAttribute"/>- and
    /// <see cref="Buildvana.Tool.CommandLine.BvArgumentAttribute"/>-decorated properties the help renderer
    /// enumerates; <see langword="null"/> for commands with no options or arguments of their own.
    /// </param>
    /// <param name="defaultVerbosity">
    /// The verbosity in effect when <c>--verbosity</c> is not given. Commands whose deliverable is their console
    /// output (query commands) use <see cref="Verbosity.Minimal"/> so that only their result, warnings, and
    /// errors are shown by default.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="aliases"/> is empty or contains an empty alias.</exception>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Lowercase is the display form of command names, not a comparison normalization.")]
    public ImplementsCommandAttribute(
        string aliases,
        bool consumesAllArguments = false,
        Type? settingsType = null,
        Verbosity defaultVerbosity = Verbosity.Normal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aliases);
        var paths = new List<IReadOnlyList<string>>();
        foreach (var alias in aliases.Split('|'))
        {
            var segments = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
            {
                throw new ArgumentException($"Alias list '{aliases}' contains an empty alias.", nameof(aliases));
            }

            paths.Add([..segments.Select(static s => s.ToLowerInvariant())]);
        }

        Aliases = aliases;
        AliasPaths = paths;
        ConsumesAllArguments = consumesAllArguments;
        SettingsType = settingsType;
        DefaultVerbosity = defaultVerbosity;
    }

    /// <summary>
    /// Gets the original alias list the attribute was constructed from.
    /// </summary>
    public string Aliases { get; }

    /// <summary>
    /// Gets the paths the command is invoked under, each as a list of segments. The first path is canonical.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> AliasPaths { get; }

    /// <summary>
    /// Gets a value indicating whether the command forwards all of its arguments verbatim.
    /// </summary>
    public bool ConsumesAllArguments { get; }

    /// <summary>
    /// Gets the command's <c>*Settings</c> type, or <see langword="null"/> if it has no options or arguments of its own.
    /// </summary>
    public Type? SettingsType { get; }

    /// <summary>
    /// Gets the verbosity in effect when <c>--verbosity</c> is not given.
    /// </summary>
    public Verbosity DefaultVerbosity { get; }
}
