// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace Buildvana.Core.MSBuild;

/// <summary>
/// Reads a string as MSBuild reads a boolean.
/// </summary>
/// <remarks>
/// <para>MSBuild reads <c>true</c>, <c>on</c>, <c>yes</c>, <c>!false</c>, <c>!off</c>, and <c>!no</c> as true,
/// and <c>false</c>, <c>off</c>, <c>no</c>, <c>!true</c>, <c>!on</c>, and <c>!yes</c> as false, in any case.
/// A condition such as <c>'$(X)' == 'true'</c> holds for every one of the true keywords. Code that reads a value
/// from MSBuild, such as a property or item metadata, uses this class to agree with the conditions that
/// set it.</para>
/// <para>The keywords come from <c>ConversionUtilities</c> in <c>src/Framework/Utilities/ConversionUtilities.cs</c>
/// of <see href="https://github.com/dotnet/msbuild">dotnet/msbuild</see>, which is internal to MSBuild.</para>
/// </remarks>
public static class MSBuildBoolean
{
    private static readonly FrozenDictionary<string, bool> Keywords = new Dictionary<string, bool>
    {
        ["true"] = true,
        ["on"] = true,
        ["yes"] = true,
        ["!false"] = true,
        ["!off"] = true,
        ["!no"] = true,
        ["false"] = false,
        ["off"] = false,
        ["no"] = false,
        ["!true"] = false,
        ["!on"] = false,
        ["!yes"] = false,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether MSBuild reads a string as true.
    /// </summary>
    /// <param name="value">The string to read.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is one of the keywords MSBuild reads as true;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsTrue(string? value) => TryParse(value, out var result) && result;

    /// <summary>
    /// Determines whether MSBuild reads a string as false.
    /// </summary>
    /// <param name="value">The string to read.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is one of the keywords MSBuild reads as false;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool IsFalse(string? value) => TryParse(value, out var result) && !result;

    /// <summary>
    /// Reads a string as MSBuild reads a boolean.
    /// </summary>
    /// <param name="value">The string to read.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the boolean MSBuild reads
    /// <paramref name="value"/> as; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if MSBuild reads <paramref name="value"/> as a boolean;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out bool result)
    {
        if (value is not null && Keywords.TryGetValue(value, out result))
        {
            return true;
        }

        result = false;
        return false;
    }
}
