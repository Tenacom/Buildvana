// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Infrastructure.Execution;

/// <summary>
/// A node in the command tree built by <see cref="CommandRegistry"/>: one path segment, the command registered
/// at this exact path (if any), and the nodes for the paths that extend it. A node can be both a command and
/// have children, e.g. when a command is aliased onto the path of a command group.
/// </summary>
/// <remarks>
/// <para>A node reached only through alias paths names another node's command rather than one of its own. It
/// dispatches like any other node, and help states it as an alias of the node it names, never as a command of
/// its own; see <see cref="CanonicalNode"/>.</para>
/// </remarks>
internal sealed class CommandNode
{
    private readonly SortedDictionary<string, CommandNode> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommandNode> _aliases = [];

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
    /// Gets the node this one is an alias of, or <see langword="null"/> when this node lies on a canonical
    /// path of its own.
    /// </summary>
    public CommandNode? CanonicalNode { get; private set; }

    /// <summary>
    /// Gets the nodes aliased onto this one, in the order their commands were registered. Empty when nothing
    /// is aliased onto this node.
    /// </summary>
    public IReadOnlyList<CommandNode> Aliases => _aliases;

    /// <summary>
    /// Gets a value indicating whether this node names another node's path rather than one of its own.
    /// </summary>
    public bool IsAlias => CanonicalNode is not null;

    /// <summary>
    /// Gets the node's name as a help listing states it: its own name, followed by the name of every node
    /// aliased onto it (e.g. <c>"dependencies, deps"</c>).
    /// </summary>
    public string DisplayName
        => _aliases.Count == 0 ? Name : Name + ", " + string.Join(", ", _aliases.Select(static a => a.Name));

    /// <summary>
    /// Gets the nodes for the paths that extend this node's path, ordered by name.
    /// </summary>
    public IReadOnlyCollection<CommandNode> Children => _children.Values;

    /// <summary>
    /// Gets a value indicating whether any registered path extends this node's path.
    /// </summary>
    public bool HasChildren => _children.Count > 0;

    /// <summary>
    /// Gets or sets a value indicating whether a command's canonical path passes through this node. Such a
    /// node carries a name of its own and is never an alias, whatever alias paths reach it.
    /// </summary>
    internal bool IsOnCanonicalPath { get; set; }

    /// <summary>
    /// Finds the direct child registered under the given segment (case-insensitive).
    /// </summary>
    /// <param name="name">The path segment.</param>
    /// <returns>The matching child node, or <see langword="null"/> if there is none.</returns>
    public CommandNode? FindChild(string name)
    {
        Guard.IsNotNullOrEmpty(name);
        return _children.GetValueOrDefault(name);
    }

    /// <summary>
    /// Records that <paramref name="alias"/> names this node's path under another name.
    /// </summary>
    /// <param name="alias">The node to record as an alias of this one.</param>
    internal void AddAlias(CommandNode alias)
    {
        alias.CanonicalNode = this;
        _aliases.Add(alias);
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
