// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace Buildvana.Core;

/// <summary>
/// A single structured problem a build step can report: a severity, a code, a message, and an optional source
/// location. Carried by <see cref="BuildFailedException"/> so a host can render each problem on its own line.
/// </summary>
/// <param name="Severity">The severity of the problem.</param>
/// <param name="Code">A diagnostic code (e.g. <c>BV1101</c>) that documents the problem.</param>
/// <param name="Message">A human-readable description of the problem.</param>
/// <param name="File">The file the problem was found in, or <see langword="null"/> if it has no location.</param>
/// <param name="Line">The 1-based line of the problem, or 0 if unknown.</param>
/// <param name="Column">The 1-based column of the problem, or 0 if unknown.</param>
public sealed record BuildDiagnostic(
    BuildDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? File = null,
    int Line = 0,
    int Column = 0)
{
    /// <summary>
    /// Returns the diagnostic in the canonical compiler/MSBuild format
    /// (<c>file(line,column): severity code: message</c>), which terminals such as VS Code render as a
    /// clickable link. The location prefix is omitted when <see cref="File"/> is <see langword="null"/>.
    /// </summary>
    /// <returns>The formatted diagnostic line.</returns>
    public override string ToString()
    {
        var severity = Severity is BuildDiagnosticSeverity.Error ? "error" : "warning";
        var location = File is null ? string.Empty
            : Line > 0 ? string.Create(CultureInfo.InvariantCulture, $"{File}({Line},{Column}): ")
            : $"{File}: ";
        return $"{location}{severity} {Code}: {Message}";
    }
}
