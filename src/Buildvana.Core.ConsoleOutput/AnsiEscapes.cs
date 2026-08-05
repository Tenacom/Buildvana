// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Core.ConsoleOutput;

/// <summary>
/// Provides ANSI (virtual terminal) escape sequences for console text styling.
/// </summary>
/// <remarks>
/// <para>Unlike <see cref="Console.ForegroundColor"/>, whose implementation is tied to the standard output
/// stream (on Unix it emits escape sequences to standard output even when the write targets standard error),
/// these sequences can be written to any stream, so the caller decides where styling goes.</para>
/// <para>The terminal attached to the target stream must interpret virtual terminal sequences; on Windows, see
/// <see cref="VirtualTerminal"/>.</para>
/// </remarks>
public static class AnsiEscapes
{
    /// <summary>
    /// The escape sequence that resets all text attributes to the terminal's defaults.
    /// </summary>
    public const string Reset = "\e[0m";

    /// <summary>
    /// Gets the escape sequence that sets the foreground to the ANSI color corresponding to the specified
    /// <see cref="ConsoleColor"/>.
    /// </summary>
    /// <param name="color">The console color to translate.</param>
    /// <returns>The escape sequence for <paramref name="color"/>.</returns>
    /// <remarks>
    /// <para>The mapping follows the same correspondence the BCL uses on Unix: the eight "dark" colors map to
    /// standard-intensity ANSI colors (SGR 30-37) and the remaining eight to high-intensity colors (SGR 90-97),
    /// so a label rendered through these sequences looks the same as one rendered by setting
    /// <see cref="Console.ForegroundColor"/>.</para>
    /// </remarks>
    public static string Foreground(ConsoleColor color)
    {
        var value = (int)color;
        if (value is < 0 or > 15)
        {
            return ThrowHelper.ThrowArgumentOutOfRangeException<string>(nameof(color), color, "Unknown console color.");
        }

        // ConsoleColor packs channels blue-lowest (B=1, G=2, R=4); SGR packs them red-lowest (R=1, G=2, B=4):
        // swap the red and blue bits, then offset into the standard- (30) or high-intensity (90) SGR range.
        var rgb = ((value & 0b100) >> 2) | (value & 0b010) | ((value & 0b001) << 2);
        var code = (value < 8 ? 30 : 90) + rgb;
        return string.Create(CultureInfo.InvariantCulture, $"\e[{code}m");
    }
}
