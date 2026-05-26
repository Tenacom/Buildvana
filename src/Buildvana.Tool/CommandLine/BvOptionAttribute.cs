// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// Declares a <c>bv</c> command-line option on a <c>*Settings</c> property: its long/short names and, for
/// value-bearing options, the value placeholder shown in help. Parsed from a Spectre-style template such as
/// <c>"-c|--configuration &lt;NAME&gt;"</c> or <c>"--no-color"</c>.
/// </summary>
/// <remarks>
/// <para>This attribute carries help metadata only. It does not drive parsing: the option reader
/// (<see cref="CliOptionReader"/>) is fed explicit names by each <c>*Settings</c> type. The help renderer
/// reflects these attributes to print the OPTIONS grid.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class BvOptionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BvOptionAttribute"/> class from an option template.
    /// </summary>
    /// <param name="template">
    /// The option template: one or more <c>'|'</c>-separated names (each <c>--long</c> or <c>-s</c>) optionally
    /// followed by a whitespace-separated value placeholder in angle brackets, e.g. <c>"-c|--configuration &lt;NAME&gt;"</c>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="template"/> is empty or declares no names.</exception>
    public BvOptionAttribute(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;

        var namesPart = template;
        var angleStart = template.IndexOf('<', StringComparison.Ordinal);
        if (angleStart >= 0)
        {
            var angleEnd = template.IndexOf('>', angleStart);
            if (angleEnd > angleStart)
            {
                ValueName = template[(angleStart + 1)..angleEnd];
            }

            namesPart = template[..angleStart];
        }

        var shortNames = new List<string>();
        var longNames = new List<string>();
        foreach (var rawName in namesPart.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawName.StartsWith("--", StringComparison.Ordinal))
            {
                longNames.Add(rawName);
            }
            else if (rawName.StartsWith('-'))
            {
                shortNames.Add(rawName);
            }
        }

        if (longNames.Count == 0 && shortNames.Count == 0)
        {
            throw new ArgumentException($"Option template '{template}' declares no names.", nameof(template));
        }

        ShortNames = [..shortNames];
        LongNames = [..longNames];
    }

    /// <summary>
    /// Gets the original option template the attribute was constructed from.
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// Gets the short names, including the leading <c>'-'</c> (e.g. <c>"-c"</c>). May be empty.
    /// </summary>
    public IReadOnlyList<string> ShortNames { get; }

    /// <summary>
    /// Gets the long names, including the leading <c>"--"</c> (e.g. <c>"--configuration"</c>). May be empty.
    /// </summary>
    public IReadOnlyList<string> LongNames { get; }

    /// <summary>
    /// Gets the value placeholder shown in help (without angle brackets), or <see langword="null"/> for a flag option.
    /// </summary>
    public string? ValueName { get; }
}
