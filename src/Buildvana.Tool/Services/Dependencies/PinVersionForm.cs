// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What a pin states where a version is expected. Only <see cref="Literal"/> is a pin <c>bv</c> manages;
/// every other form is reported and left alone.
/// </summary>
internal enum PinVersionForm
{
    /// <summary>An exact version, e.g. <c>13.0.3</c>: the only form an automatic update moves.</summary>
    Literal,

    /// <summary>
    /// The bracket notation for one exact version, e.g. <c>[13.0.4]</c>. NuGet reads it as the version it
    /// names, and the report suggests writing it without brackets.
    /// </summary>
    BracketExact,

    /// <summary>A version range, e.g. <c>[1.0,2.0)</c>: whoever wrote it decides what resolves.</summary>
    Range,

    /// <summary>
    /// A floating version, e.g. <c>1.*</c>: restore picks the highest match every time it runs, so updating
    /// is already someone else's job.
    /// </summary>
    Floating,

    /// <summary>Text NuGet reads as neither a version nor a range.</summary>
    Unrecognized,
}
