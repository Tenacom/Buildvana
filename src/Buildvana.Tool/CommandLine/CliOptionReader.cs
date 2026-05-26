// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Buildvana.Core;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// Reads named options out of a flat token list. Each <c>ReadXxx</c> call pulls the matching tokens out of the
/// working set, so after a command has read every option it recognizes, <see cref="Remaining"/> holds whatever
/// was not consumed — empty for a clean parse, non-empty when the user passed something the command does not
/// understand.
/// </summary>
/// <remarks>
/// <para>Matching is case-insensitive across long and short names. Value options accept both the inline
/// <c>--name=value</c> form and the space-separated <c>--name value</c> form. When a name appears more than once
/// the last value wins, mirroring the pre-existing global-option behavior.</para>
/// </remarks>
internal sealed class CliOptionReader
{
    private readonly List<string> _tokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliOptionReader"/> class over a copy of the given tokens.
    /// </summary>
    /// <param name="tokens">The tokens to read from.</param>
    public CliOptionReader(IReadOnlyList<string> tokens)
    {
        Guard.IsNotNull(tokens);
        _tokens = [..tokens];
    }

    /// <summary>
    /// Gets the tokens not yet consumed by a <c>ReadXxx</c> call, in their original order.
    /// </summary>
    public IReadOnlyList<string> Remaining => _tokens;

    /// <summary>
    /// Reads a boolean flag, removing every occurrence from the working set.
    /// </summary>
    /// <param name="longName">The long name, including the leading <c>"--"</c>.</param>
    /// <param name="shortName">The short name, including the leading <c>'-'</c>, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the flag was present; otherwise, <see langword="false"/>.</returns>
    public bool ReadFlag(string longName, string? shortName = null)
    {
        Guard.IsNotNullOrEmpty(longName);
        var found = false;
        var i = 0;
        while (i < _tokens.Count)
        {
            if (MatchesExact(_tokens[i], longName, shortName))
            {
                found = true;
                _tokens.RemoveAt(i);
                continue;
            }

            i++;
        }

        return found;
    }

    /// <summary>
    /// Reads a value option, removing every occurrence (and the consumed value for the space-separated form)
    /// from the working set.
    /// </summary>
    /// <param name="longName">The long name, including the leading <c>"--"</c>.</param>
    /// <param name="shortName">The short name, including the leading <c>'-'</c>, or <see langword="null"/>.</param>
    /// <returns>The last value supplied for the option, or <see langword="null"/> if it was absent.</returns>
    /// <exception cref="BuildFailedException">The option was given in space-separated form with no following value.</exception>
    public string? ReadValue(string longName, string? shortName = null)
    {
        Guard.IsNotNullOrEmpty(longName);
        string? result = null;
        var i = 0;
        while (i < _tokens.Count)
        {
            var token = _tokens[i];
            if (MatchesExact(token, longName, shortName))
            {
                if (i + 1 >= _tokens.Count)
                {
                    throw new BuildFailedException($"Option '{token}' requires a value.");
                }

                result = _tokens[i + 1];
                _tokens.RemoveRange(i, 2);
                continue;
            }

            if (TryMatchInline(token, longName, shortName, out var inlineValue))
            {
                result = inlineValue;
                _tokens.RemoveAt(i);
                continue;
            }

            i++;
        }

        return result;
    }

    private static bool MatchesExact(string token, string longName, string? shortName)
    {
        var isLong = string.Equals(token, longName, StringComparison.OrdinalIgnoreCase);
        var isShort = shortName is not null && string.Equals(token, shortName, StringComparison.OrdinalIgnoreCase);
        return isLong || isShort;
    }

    private static bool TryMatchInline(string token, string longName, string? shortName, out string value)
    {
        var longPrefix = longName + "=";
        if (token.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = token[longPrefix.Length..];
            return true;
        }

        if (shortName is not null)
        {
            var shortPrefix = shortName + "=";
            if (token.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = token[shortPrefix.Length..];
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
