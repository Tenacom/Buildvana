// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.Versioning;

/// <summary>
/// Provides extension methods for <see cref="VersionSpec"/> instances.
/// </summary>
#pragma warning disable CA1034 // Nested types should not be visible — false positive on C# 14 extension blocks; fixed in .NET 11, backport to .NET 10 requested in https://github.com/dotnet/sdk/issues/53984
#pragma warning disable CA1708 // Identifiers should differ by more than case — false positive on classes with C# 14 extension blocks; fixed in .NET 11, https://github.com/dotnet/sdk/issues/51716
public static partial class VersionSpecExtensions
{
    extension(VersionSpec)
    {
        /// <summary>
        /// Parses a version specification in <c>MAJOR.MINOR[-[tag]]</c> format.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <returns>A newly-created <see cref="VersionSpec"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="str"/> is <see langword="null"/>.</exception>
        /// <exception cref="BuildFailedException"><paramref name="str"/> is not a valid version specification.</exception>
        /// <remarks>
        /// <para>The presence of <c>-</c> after the minor version marks a prerelease line; the tag after it is
        /// optional, restricted to ASCII letters and digits, and informational (its text is not captured).
        /// There is no <c>v</c> prefix. Trailing whitespace is tolerated; anything else after the
        /// specification is not.</para>
        /// </remarks>
        public static VersionSpec Parse(string str)
        {
            Guard.IsNotNull(str);
            BuildFailedException.ThrowIfNot(
                VersionSpec.TryParse(str, out var result),
                $"Invalid version specification '{str.Trim()}'.");
            return result;
        }

        /// <summary>
        /// Attempts to parse a version specification in <c>MAJOR.MINOR[-[tag]]</c> format.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <param name="result">When this method returns <see langword="true"/>, a newly-created
        /// <see cref="VersionSpec"/>. This parameter is passed uninitialized.</param>
        /// <returns><see langword="true"/> if successful, <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// <para>See <see cref="Parse"/> for the accepted format.</para>
        /// </remarks>
        public static bool TryParse([NotNullWhen(true)] string? str, [MaybeNullWhen(false)] out VersionSpec result)
        {
            result = null;
            if (str is null)
            {
                return false;
            }

            var match = VersionSpecRegex.Match(str);
            if (!match.Success)
            {
                return false;
            }

            // The regex only lets digit runs through, so a failed parse can only mean int overflow.
            var hasMajor = int.TryParse(match.Groups["Major"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var major);
            var hasMinor = int.TryParse(match.Groups["Minor"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var minor);
            if (!hasMajor || !hasMinor)
            {
                return false;
            }

            result = new(major, minor, match.Groups["Prerelease"].Success);
            return true;
        }
    }
}
