// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace Buildvana.Core.IO;

/// <summary>
/// Provides extension methods for <see cref="CaseSensitivityMode"/> values.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static class CaseSensitivityModeExtensions
{
    extension(CaseSensitivityMode @this)
    {
        /// <summary>
        /// Resolves this mode to its effective behavior.
        /// </summary>
        /// <returns><see langword="true"/> when matching ignores letter case;
        /// <see langword="false"/> otherwise.</returns>
        /// <exception cref="ArgumentOutOfRangeException">This value is not a known
        /// <see cref="CaseSensitivityMode"/>.</exception>
        /// <remarks>
        /// <para><see cref="CaseSensitivityMode.SystemDefault"/> resolves to case-insensitive on Windows and
        /// macOS and to case-sensitive elsewhere, mirroring the value Git probes into <c>core.ignoreCase</c>
        /// on the file systems those platforms use by default.</para>
        /// </remarks>
        public bool IgnoresCase()
        {
            return @this switch
            {
                CaseSensitivityMode.SystemDefault => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(),
                CaseSensitivityMode.CaseSensitive => false,
                CaseSensitivityMode.CaseInsensitive => true,
                _ => ThrowUnknownMode(@this),
            };

            // The exception names the offending value "mode": nameof(@this) would yield "this", which names
            // the parameter the compiler emits rather than anything a caller can see.
            static bool ThrowUnknownMode(CaseSensitivityMode mode)
                => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown case sensitivity mode.");
        }
    }
}
