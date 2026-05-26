// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Discovers <c>bv</c> commands (classes marked with <see cref="ImplementsCommandAttribute"/>) by reflection
/// and is the single authority for command lookup, display order, and forwarding metadata.
/// </summary>
/// <remarks>
/// <para>Display order follows <see cref="PipelineCommandNames"/>: the build pipeline, in execution order.
/// Commands not listed there (e.g. <c>release</c>) are ordered after the pipeline, by name.</para>
/// </remarks>
internal static class CommandRegistry
{
    // The build pipeline, in execution order. Defines the order pipeline commands appear in `bv`'s help.
    // Non-pipeline commands are appended after, ordered by name.
    private static readonly string[] PipelineCommandNames = ["clean", "restore", "build", "test", "pack"];

    /// <summary>
    /// Gets the discovered commands, ordered for help display.
    /// </summary>
    public static IReadOnlyList<CommandRegistration> Commands { get; } = Discover();

    /// <summary>
    /// Finds the command registered under the given name (case-insensitive).
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <returns>The matching <see cref="CommandRegistration"/>, or <see langword="null"/> if there is none.</returns>
    public static CommandRegistration? Find(string name)
    {
        Guard.IsNotNullOrEmpty(name);
        return Commands.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<CommandRegistration> Discover()
    {
        var discovered = new List<CommandRegistration>();
        foreach (var type in typeof(CommandRegistry).Assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<ImplementsCommandAttribute>();
            if (attribute is not null)
            {
                discovered.Add(new CommandRegistration(attribute.Name, type, attribute.ConsumesAllArguments, attribute.SettingsType));
            }
        }

        // Fail fast on a pipeline name with no implementing class (a typo in PipelineCommandNames).
        foreach (var name in PipelineCommandNames)
        {
            var implemented = discovered.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (!implemented)
            {
                throw new InvalidOperationException($"Pipeline command '{name}' has no class marked with [ImplementsCommand(\"{name}\")].");
            }
        }

        return [..discovered.OrderBy(PipelineIndexOf).ThenBy(static c => c.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static int PipelineIndexOf(CommandRegistration command)
    {
        var index = Array.FindIndex(PipelineCommandNames, n => string.Equals(n, command.Name, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }
}
