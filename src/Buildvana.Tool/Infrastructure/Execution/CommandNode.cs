// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// A node in the command tree built by <see cref="CommandRegistry"/>: one path segment, the command registered
/// at this exact path (if any), and the nodes for the paths that extend it. A node can be both a command and
/// have children, e.g. when a command is aliased onto the path of a command group.
/// </summary>
internal sealed class CommandNode
{
    private readonly SortedDictionary<string, CommandNode> _children = new(StringComparer.OrdinalIgnoreCase);

    internal CommandNode(string name, string fullName)
    {
        Name = name;
        FullName = fullName;
    }

    /// <summary>
    /// Gets the path segment this node represents (e.g. <c>"advance"</c>).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the full space-joined path of this node (e.g. <c>"version advance"</c>).
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// Gets the command registered at this exact path, or <see langword="null"/> for a pure command group.
    /// </summary>
    public CommandRegistration? Command { get; internal set; }

    /// <summary>
    /// Gets the node's description for help pages, as resolved by <see cref="CommandRegistry"/>.
    /// </summary>
    public string Description { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the nodes for the paths that extend this node's path, ordered by name.
    /// </summary>
    public IReadOnlyCollection<CommandNode> Children => _children.Values;

    /// <summary>
    /// Gets a value indicating whether any registered path extends this node's path.
    /// </summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>
    /// Finds the direct child registered under the given segment (case-insensitive).
    /// </summary>
    /// <param name="name">The path segment.</param>
    /// <returns>The matching child node, or <see langword="null"/> if there is none.</returns>
    public CommandNode? FindChild(string name)
    {
        Guard.IsNotNullOrEmpty(name);
        return _children.TryGetValue(name, out var child) ? child : null;
    }

    internal CommandNode GetOrAddChild(string name)
    {
        if (!_children.TryGetValue(name, out var child))
        {
            child = new(name, FullName.Length == 0 ? name : FullName + " " + name);
            _children.Add(name, child);
        }

        return child;
    }
}
