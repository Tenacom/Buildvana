// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Text;

namespace Buildvana.Core.JsonSchema;

/// <summary>
/// Describes a single JSON Schema validation failure.
/// </summary>
/// <param name="Kind">The kind of failure, for callers that map errors to their own diagnostics.</param>
/// <param name="JsonPointer">
/// An RFC 6901 JSON Pointer locating the offending value, or an empty string for the document root. This is the
/// stable, unambiguous key used to look the value up in a <see cref="JsonSourceMap"/>.
/// </param>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Line">The 1-based source line of the offending value, or 0 when it has not been resolved.</param>
/// <param name="Column">The 1-based source column of the offending value, or 0 when it has not been resolved.</param>
public sealed record JsonSchemaValidationError(
    JsonSchemaErrorKind Kind,
    string JsonPointer,
    string Message,
    int Line = 0,
    int Column = 0)
{
    /// <summary>
    /// Gets a human-friendly dotted path to the offending value, derived from <see cref="JsonPointer"/>
    /// (an empty string for the document root). Intended for display only; use <see cref="JsonPointer"/> as a key.
    /// </summary>
    public string DisplayPath => ToDisplayPath(JsonPointer);

    /// <summary>
    /// Returns a string combining the display path and the message.
    /// </summary>
    /// <returns>The message alone for a root-level error, or <c>"{DisplayPath}: {Message}"</c> otherwise.</returns>
    public override string ToString()
    {
        var displayPath = DisplayPath;
        return displayPath.Length == 0 ? Message : $"{displayPath}: {Message}";
    }

    private static string ToDisplayPath(string pointer)
    {
        if (pointer.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var rawToken in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            // RFC 6901 unescaping: decode "~1" to "/" first, then "~0" to "~".
            var token = rawToken
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            var isArrayIndex = token.Length > 0 && token.All(char.IsAsciiDigit);
            if (isArrayIndex)
            {
                _ = builder.Append('[').Append(token).Append(']');
            }
            else
            {
                if (builder.Length > 0)
                {
                    _ = builder.Append('.');
                }

                _ = builder.Append(token);
            }
        }

        return builder.ToString();
    }
}
