// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Buildvana.Core.IO;

/// <summary>
/// Provides extension methods for <see cref="MatchCasing"/> values.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class MatchCasingExtensions
{
    extension(MatchCasing @this)
    {
        /// <summary>
        /// Tells whether this casing mode compares case-insensitively.
        /// <see cref="MatchCasing.PlatformDefault"/> resolves to the platform's convention:
        /// case-insensitive on Windows and macOS, case-sensitive elsewhere.
        /// </summary>
        /// <returns><see langword="true"/> if the mode compares case-insensitively;
        /// <see langword="false"/> otherwise.</returns>
        public bool IsCaseInsensitive() => @this switch
        {
            MatchCasing.CaseSensitive => false,
            MatchCasing.CaseInsensitive => true,
            MatchCasing.PlatformDefault => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(),
            _ => throw new ArgumentOutOfRangeException(nameof(@this)),
        };
    }
}
