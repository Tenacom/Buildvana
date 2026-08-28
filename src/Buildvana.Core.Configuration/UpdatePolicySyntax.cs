// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Buildvana.Core.Configuration;

// The policy string syntax, shared by the two policy types: a lowercase kind name, optionally followed by a
// '-' meaning "prerelease versions are allowed". Kind names match case-insensitively, like the enum values
// elsewhere in the configuration file.
internal static class UpdatePolicySyntax
{
    // Every policy string of a kind enum, in enum order, each kind followed by its prerelease form. The
    // schema needs the set as a constant, so it is spelled out rather than derived; the schema tests derive
    // the same lists from the enums and compare, which is what keeps the two from drifting apart.
    public const string PackagePolicyValues =
        "disable, disable-, exact, exact-, revision, revision-, patch, patch-, minor, minor-, major, major-";

    public const string NetSdkPolicyValues =
        "disable, disable-, patch, patch-, feature, feature-, minor, minor-, major, major-, lts, lts-";

    private const char AllowPrereleaseSuffix = '-';

    // Both directions key on the enum member names, so that the wire form of a kind has one definition.
    public static bool TryParse<TKind>(string? text, out TKind kind, out bool allowPrerelease)
        where TKind : struct, Enum
    {
        kind = default;
        allowPrerelease = false;
        if (text is null)
        {
            return false;
        }

        var hasSuffix = text.EndsWith(AllowPrereleaseSuffix);
        var name = hasSuffix ? text[..^1] : text;

        // Enum.TryParse also accepts numeric text ("1") and comma-separated lists ("minor,major"). Neither is
        // a policy string, and accepting either would turn a typo into a value nobody meant to write.
        if (!IsAllAsciiLetters(name) || !Enum.TryParse(name, ignoreCase: true, out kind))
        {
            return false;
        }

        allowPrerelease = hasSuffix;
        return true;
    }

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Lowercase is the wire form of a policy string, not a comparison normalization.")]
    public static string Format<TKind>(TKind kind, bool allowPrerelease)
        where TKind : struct, Enum
    {
        var name = kind.ToString().ToLowerInvariant();
        return allowPrerelease ? name + AllowPrereleaseSuffix : name;
    }

    private static bool IsAllAsciiLetters(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!char.IsAsciiLetter(c))
            {
                return false;
            }
        }

        return true;
    }
}
