// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Buildvana.Core;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// Discovers <c>bv</c> commands (classes marked with <see cref="ImplementsCommandAttribute"/>) by reflection,
/// arranges their alias paths in a tree of <see cref="CommandNode"/>s, and is the single authority for command
/// lookup, display order, forwarding metadata, and the descriptions of pure command-group nodes.
/// </summary>
/// <remarks>
/// <para>Top-level display order follows <see cref="PipelineCommandNames"/>: the build pipeline, in execution
/// order. Nodes not listed there (e.g. <c>release</c>, <c>version</c>) are ordered after the pipeline, by name.
/// Deeper nodes are always ordered by name.</para>
/// </remarks>
internal static class CommandRegistry
{
    // The build pipeline, in execution order. Defines the order pipeline commands appear in `bv`'s help.
    // Non-pipeline nodes are appended after, ordered by name.
    private static readonly string[] PipelineCommandNames = ["clean", "restore", "build", "test", "pack"];

    // Descriptions for tree nodes whose own command description does not fit the node (or that have no command
    // at all), keyed by full node path. Typically command-group nodes: a group aliased onto one of its
    // subcommands would otherwise inherit that subcommand's description.
    private static readonly Dictionary<string, string> NodeDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["version"] = "Show or advance the project's version spec.",
    };

    /// <summary>
    /// Gets the discovered commands, ordered by canonical path for help display.
    /// </summary>
    public static IReadOnlyList<CommandRegistration> Commands { get; } = Discover();

    /// <summary>
    /// Gets the top-level nodes of the command tree, ordered for help display.
    /// </summary>
    public static IReadOnlyList<CommandNode> TopLevelNodes { get; } = BuildTreeAndValidateDescriptions();

    /// <summary>
    /// Finds the command registered under the given path (case-insensitive), canonical or alias.
    /// </summary>
    /// <param name="path">The space-separated command path (e.g. <c>"build"</c>, <c>"version advance"</c>).</param>
    /// <returns>The matching <see cref="CommandRegistration"/>, or <see langword="null"/> if there is none.</returns>
    public static CommandRegistration? Find(string path)
    {
        Guard.IsNotNullOrEmpty(path);
        return FindNode(path)?.Command;
    }

    /// <summary>
    /// Resolves a subcommand and its positional parameters against the command tree, walking down the tree as
    /// long as positionals match child nodes.
    /// </summary>
    /// <param name="subcommand">The subcommand: the first non-option token on the command line.</param>
    /// <param name="positionals">The positional tokens following the subcommand.</param>
    /// <returns>The deepest matched node, and the positionals left over after the walk.</returns>
    /// <exception cref="BuildFailedException">
    /// <paramref name="subcommand"/> matches no top-level node, or a leftover positional follows a node that
    /// has subcommands but none matching it.
    /// </exception>
    public static (CommandNode Node, IReadOnlyList<string> RemainingPositionals) Resolve(string subcommand, IReadOnlyList<string> positionals)
    {
        Guard.IsNotNullOrEmpty(subcommand);
        Guard.IsNotNull(positionals);
        var node = FindTopLevel(subcommand)
            ?? throw new BuildFailedException($"Unknown command '{subcommand}'. Run 'bv --help' for the list of commands.");

        var index = 0;
        while (index < positionals.Count)
        {
            var child = node.FindChild(positionals[index]);
            if (child is null)
            {
                break;
            }

            node = child;
            index++;
        }

        // At a node with subcommands, an unmatched token is a typo, not a positional parameter: a command
        // aliased onto a group node never receives positionals through that alias.
        if (index < positionals.Count && node.HasChildren)
        {
            var valid = string.Join(", ", node.Children.Select(static c => c.Name));
            throw new BuildFailedException($"Unknown subcommand '{positionals[index]}' for 'bv {node.FullName}'. Valid subcommands: {valid}.");
        }

        return (node, index == 0 ? positionals : [..positionals.Skip(index)]);
    }

    /// <summary>
    /// Builds a command tree from the given commands, failing fast on duplicate paths, and returns its top-level
    /// nodes ordered for help display. Exposed to tests; production code uses <see cref="TopLevelNodes"/>.
    /// </summary>
    /// <param name="commands">The commands to arrange in a tree.</param>
    /// <returns>The top-level nodes of the tree.</returns>
    /// <exception cref="InvalidOperationException">Two commands register the same path.</exception>
    internal static IReadOnlyList<CommandNode> BuildTree(IReadOnlyList<CommandRegistration> commands)
    {
        var root = new CommandNode(string.Empty, string.Empty);
        foreach (var command in commands)
        {
            foreach (var path in command.AliasPaths)
            {
                var node = root;
                foreach (var segment in path)
                {
                    node = node.GetOrAddChild(segment);
                }

                if (node.Command is not null)
                {
                    throw new InvalidOperationException(
                        $"Command path '{node.FullName}' is registered by both {node.Command.CommandType.Name} and {command.CommandType.Name}.");
                }

                node.Command = command;
            }
        }

        AssignDescriptions(root);
        return [..root.Children.OrderBy(NodePipelineIndexOf).ThenBy(static n => n.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static CommandNode? FindTopLevel(string name)
        => TopLevelNodes.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

    private static CommandNode? FindNode(string path) => FindNodeIn(TopLevelNodes, path);

    private static IReadOnlyList<CommandRegistration> Discover()
    {
        var discovered = new List<CommandRegistration>();
        foreach (var type in typeof(CommandRegistry).Assembly.GetTypes())
        {
            var attribute = type.GetCustomAttribute<ImplementsCommandAttribute>();
            if (attribute is not null)
            {
                discovered.Add(new CommandRegistration(
                    attribute.AliasPaths,
                    type,
                    attribute.ConsumesAllArguments,
                    attribute.SettingsType,
                    attribute.DefaultVerbosity));
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

    private static IReadOnlyList<CommandNode> BuildTreeAndValidateDescriptions()
    {
        var topLevelNodes = BuildTree(Commands);

        // Fail fast on a description for a path no command tree node matches (a typo in NodeDescriptions).
        foreach (var path in NodeDescriptions.Keys)
        {
            if (FindNodeIn(topLevelNodes, path) is null)
            {
                throw new InvalidOperationException($"NodeDescriptions contains an entry for '{path}', which matches no command tree node.");
            }
        }

        return topLevelNodes;
    }

    private static CommandNode? FindNodeIn(IReadOnlyList<CommandNode> topLevelNodes, string path)
    {
        var segments = path.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var node = topLevelNodes.FirstOrDefault(n => string.Equals(n.Name, segments[0], StringComparison.OrdinalIgnoreCase));
        foreach (var segment in segments.Skip(1))
        {
            if (node is null)
            {
                return null;
            }

            node = node.FindChild(segment);
        }

        return node;
    }

    private static void AssignDescriptions(CommandNode node)
    {
        foreach (var child in node.Children)
        {
            child.Description = DescribeNode(child);
            AssignDescriptions(child);
        }
    }

    private static string DescribeNode(CommandNode node)
    {
        if (NodeDescriptions.TryGetValue(node.FullName, out var description))
        {
            return description;
        }

        if (node.Command is not null)
        {
            return node.Command.CommandType.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
        }

        return $"Subcommands: {string.Join(", ", node.Children.Select(static c => c.Name))}.";
    }

    private static int PipelineIndexOf(CommandRegistration command) => PipelineIndexOf(command.CanonicalPath[0]);

    private static int NodePipelineIndexOf(CommandNode node) => PipelineIndexOf(node.Name);

    private static int PipelineIndexOf(string name)
    {
        var index = Array.FindIndex(PipelineCommandNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }
}
