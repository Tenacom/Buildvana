// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// Declares a <c>bv</c> positional command-line argument on a <c>*Settings</c> property: its name, whether it
/// is required, and whether it takes every remaining positional. Parsed from a template such as
/// <c>"&lt;NAME&gt;"</c> (required) or <c>"[NAME]"</c> (optional), with a <c>"..."</c> before the closing
/// bracket for a variadic argument.
/// </summary>
/// <remarks>
/// <para>This attribute carries help and validation metadata only. It does not drive parsing: each
/// <c>*Settings</c> type binds its positionals explicitly. The help renderer reflects these attributes to print
/// the usage line and the ARGUMENTS grid; the argument validator reflects them to bound the number of
/// positionals a command accepts. Multiple arguments bind in property declaration order, which must list the
/// required ones first; <see cref="Infrastructure.Execution.CommandRegistry"/> fails fast at discovery time
/// when a required argument follows an optional one.</para>
/// <para>A variadic argument takes every positional the ones before it left, so a command declaring one
/// accepts any number of them. It must be the last argument declared, and the registry fails fast at
/// discovery time on an argument declared after it. A required variadic argument takes at least one
/// positional, an optional one any number including none.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class BvArgumentAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BvArgumentAttribute"/> class from an argument template.
    /// </summary>
    /// <param name="template">
    /// The argument template: the argument name in angle brackets (<c>"&lt;NAME&gt;"</c>) for a required
    /// argument, or in square brackets (<c>"[NAME]"</c>) for an optional one. A <c>"..."</c> after the name
    /// (<c>"[NAME...]"</c>) declares the argument variadic.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="template"/> is empty, malformed, or declares an empty name.</exception>
    public BvArgumentAttribute(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;

        var trimmed = template.Trim();
        var required = trimmed.StartsWith('<') && trimmed.EndsWith('>');
        var optional = trimmed.StartsWith('[') && trimmed.EndsWith(']');
        var inner = required || optional ? trimmed[1..^1].Trim() : string.Empty;
        var variadic = inner.EndsWith("...", StringComparison.Ordinal);
        var name = (variadic ? inner[..^3] : inner).TrimEnd();
        if (name.Length == 0)
        {
            throw new ArgumentException(
                $"Argument template '{template}' must be of the form '<NAME>' (required argument) or '[NAME]' (optional argument), "
                + "with an optional '...' after the name for a variadic argument.",
                nameof(template));
        }

        Name = name;
        Required = required;
        Variadic = variadic;
    }

    /// <summary>
    /// Gets the original argument template the attribute was constructed from.
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// Gets the argument name shown in help (without brackets).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the argument is required.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets a value indicating whether the argument takes every positional the arguments before it left.
    /// </summary>
    public bool Variadic { get; }
}
