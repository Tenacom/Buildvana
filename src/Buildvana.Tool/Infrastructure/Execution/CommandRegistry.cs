// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Spectre.Console.Cli;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Discovers <c>bv</c> commands (classes marked with <see cref="ImplementsCommandAttribute"/>) by reflection
/// and is the single authority for registering them with Spectre and answering forwarding questions.
/// </summary>
/// <remarks>
/// <para>Command display/registration order follows <see cref="PipelineCommandNames"/>: the build pipeline, in
/// execution order. Commands not listed there (e.g. <c>release</c>) are non-pipeline commands and are ordered
/// after the pipeline, by name.</para>
/// </remarks>
internal static class CommandRegistry
{
    // The build pipeline, in execution order. Defines the order pipeline commands are registered (hence the
    // order they appear in `bv`'s help). Non-pipeline commands are appended after, ordered by name.
    private static readonly string[] PipelineCommandNames = ["clean", "restore", "build", "test", "pack"];

    private static readonly MethodInfo AddCommandMethod = FindAddCommandMethod();

    /// <summary>
    /// Gets the discovered commands, ordered for registration and help display.
    /// </summary>
    public static IReadOnlyList<CommandRegistration> Commands { get; } = Discover();

    /// <summary>
    /// Registers every discovered command with the supplied Spectre configurator, in order.
    /// </summary>
    /// <param name="config">The Spectre configurator.</param>
    public static void RegisterAll(IConfigurator config)
    {
        Guard.IsNotNull(config);
        foreach (var command in Commands)
        {
            _ = AddCommandMethod.MakeGenericMethod(command.CommandType).Invoke(config, [command.Name]);
        }
    }

    /// <summary>
    /// Tells whether the command with the given name forwards all of its arguments verbatim.
    /// </summary>
    /// <param name="commandName">The command name.</param>
    /// <returns><see langword="true"/> if the command consumes and forwards all of its arguments; otherwise, <see langword="false"/>.</returns>
    public static bool ConsumesAllArguments(string commandName)
    {
        Guard.IsNotNullOrEmpty(commandName);
        return Commands.Any(c => string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase) && c.ConsumesAllArguments);
    }

    private static IReadOnlyList<CommandRegistration> Discover()
    {
        var discovered = new List<CommandRegistration>();
        foreach (var type in typeof(CommandRegistry).Assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<ImplementsCommandAttribute>();
            if (attribute is not null)
            {
                discovered.Add(new CommandRegistration(attribute.Name, type, attribute.ConsumesAllArguments));
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

    private static MethodInfo FindAddCommandMethod()
    {
        foreach (var method in typeof(IConfigurator).GetMethods())
        {
            if (!method.IsGenericMethodDefinition || method.Name != nameof(IConfigurator.AddCommand))
            {
                continue;
            }

            var parameters = method.GetParameters();
            var isMatch = method.GetGenericArguments().Length == 1
                && parameters.Length == 1
                && parameters[0].ParameterType == typeof(string);
            if (isMatch)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Could not locate IConfigurator.AddCommand<TCommand>(string) via reflection.");
    }
}
