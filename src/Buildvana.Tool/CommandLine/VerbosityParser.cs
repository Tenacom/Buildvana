// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.CommandLine;

/// <summary>
/// Parses the value of the <c>--verbosity</c> global option.
/// </summary>
internal static class VerbosityParser
{
    /// <summary>
    /// Parses a raw <c>--verbosity</c> value into a <see cref="Verbosity"/>.
    /// </summary>
    /// <param name="raw">The raw option value, in any casing.</param>
    /// <returns>The corresponding <see cref="Verbosity"/>.</returns>
    /// <exception cref="BuildFailedException"><paramref name="raw"/> is not a recognized verbosity level.</exception>
    public static Verbosity Parse(string raw)
    {
        Guard.IsNotNull(raw);
        return raw.ToUpperInvariant() switch
        {
            "QUIET" or "Q" => Verbosity.Quiet,
            "MINIMAL" or "M" => Verbosity.Minimal,
            "NORMAL" or "N" => Verbosity.Normal,
            "DETAILED" or "D" => Verbosity.Detailed,
            "DIAGNOSTIC" or "DIAG" => Verbosity.Diagnostic,
            _ => throw new BuildFailedException($"Unknown verbosity level '{raw}'. Use one of: [q]uiet, [m]inimal, [n]ormal, [d]etailed, [diag]nostic."),
        };
    }
}
