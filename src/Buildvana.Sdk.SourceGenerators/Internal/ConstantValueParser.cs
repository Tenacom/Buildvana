// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Buildvana.Sdk.SourceGenerators.Internal;

/// <summary>
/// Parses constant values expressed in the syntax documented in <c>docs/ConstantsSyntax.md</c>:
/// an empty string yields <see langword="null"/>; a double-quoted string (with inner double quotes doubled)
/// yields the quoted text; a <c>type:value</c> pair yields a value of the specified type;
/// anything else is parsed by guessing the type (int, then long, then bool, then string).
/// </summary>
internal static class ConstantValueParser
{
    private static readonly Dictionary<string, Type> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Byte"] = typeof(byte),
        ["byte"] = typeof(byte),
        ["uint8"] = typeof(byte),
        ["System.Int16"] = typeof(short),
        ["short"] = typeof(short),
        ["int16"] = typeof(short),
        ["System.Int32"] = typeof(int),
        ["int"] = typeof(int),
        ["int32"] = typeof(int),
        ["System.Int64"] = typeof(long),
        ["long"] = typeof(long),
        ["int64"] = typeof(long),
        ["System.Boolean"] = typeof(bool),
        ["bool"] = typeof(bool),
        ["System.String"] = typeof(string),
        ["string"] = typeof(string),
    };

    public static bool TryParse(string? str, out object? result)
    {
        if (string.IsNullOrEmpty(str))
        {
            result = null;
            return true;
        }

        if (str!.Length > 1 && str[0] == '"' && str[^1] == '"')
        {
            result = str.Substring(1, str.Length - 2).Replace("\"\"", "\"");
            return true;
        }

        var colonPos = str.IndexOf(':');
        return colonPos < 1
            ? TryParseGuessingType(str, out result)
            : TryParseTyped(str.Substring(0, colonPos), str.Substring(colonPos + 1), out result);
    }

    private static bool TryParseGuessingType(string str, out object? result)
    {
        if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            result = parsedInt;
            return true;
        }

        if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
        {
            result = parsedLong;
            return true;
        }

        if (bool.TryParse(str, out var parsedBool))
        {
            result = parsedBool;
            return true;
        }

        result = str;
        return true;
    }

    private static bool TryParseTyped(string typeStr, string str, out object? result)
    {
        if (!AllowedTypes.TryGetValue(typeStr.Trim(), out var type))
        {
            result = null;
            return false;
        }

        try
        {
            result = Convert.ChangeType(str, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException)
        {
            result = null;
            return false;
        }
    }
}
