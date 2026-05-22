// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Buildvana.Tool.Cli;

/// <summary>
/// Reflection-derived set of command names (as registered with Spectre, matched case-insensitively) whose
/// classes are marked with <see cref="ConsumeAllArgumentsAttribute"/>.
/// </summary>
/// <remarks>
/// <para>Shared by <c>Program.Main</c> (to decide whether to stash a command's arguments for verbatim
/// forwarding) and <see cref="BvHelpProvider"/> (to render the forwarding note in help output).</para>
/// </remarks>
internal static class ConsumeAllArgumentsCommands
{
    /// <summary>
    /// Gets the set of command names that consume and forward all of their arguments.
    /// </summary>
    public static IReadOnlySet<string> Names { get; } = Build();

    private static HashSet<string> Build()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(ConsumeAllArgumentsCommands).Assembly.GetTypes())
        {
            if (type.Namespace is null || (type.Namespace != "Buildvana.Tool.Cli" && !type.Namespace.StartsWith("Buildvana.Tool.Cli.", StringComparison.Ordinal)))
            {
                continue;
            }

            if (!type.Name.EndsWith("Command", StringComparison.Ordinal))
            {
                continue;
            }

            if (type.GetCustomAttribute<ConsumeAllArgumentsAttribute>() is not null)
            {
                result.Add(type.Name[..^"Command".Length]);
            }
        }

        return result;
    }
}
