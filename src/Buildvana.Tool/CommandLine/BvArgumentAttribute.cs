// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// Declares a <c>bv</c> positional command-line argument on a <c>*Settings</c> property: its name and whether
/// it is required. Parsed from a template such as <c>"&lt;NAME&gt;"</c> (required) or <c>"[NAME]"</c> (optional).
/// </summary>
/// <remarks>
/// <para>This attribute carries help and validation metadata only. It does not drive parsing: each
/// <c>*Settings</c> type binds its positionals explicitly. The help renderer reflects these attributes to print
/// the usage line and the ARGUMENTS grid; the argument validator reflects them to bound the number of
/// positionals a command accepts. Multiple arguments bind in property declaration order, which must list the
/// required ones first; <see cref="Infrastructure.Execution.CommandRegistry"/> fails fast at discovery time
/// when a required argument follows an optional one.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class BvArgumentAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BvArgumentAttribute"/> class from an argument template.
    /// </summary>
    /// <param name="template">
    /// The argument template: the argument name in angle brackets (<c>"&lt;NAME&gt;"</c>) for a required
    /// argument, or in square brackets (<c>"[NAME]"</c>) for an optional one.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="template"/> is empty, malformed, or declares an empty name.</exception>
    public BvArgumentAttribute(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;

        var trimmed = template.Trim();
        var required = trimmed.StartsWith('<') && trimmed.EndsWith('>');
        var optional = trimmed.StartsWith('[') && trimmed.EndsWith(']');
        var name = required || optional ? trimmed[1..^1].Trim() : string.Empty;
        if (name.Length == 0)
        {
            throw new ArgumentException(
                $"Argument template '{template}' must be of the form '<NAME>' (required argument) or '[NAME]' (optional argument).",
                nameof(template));
        }

        Name = name;
        Required = required;
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
}
