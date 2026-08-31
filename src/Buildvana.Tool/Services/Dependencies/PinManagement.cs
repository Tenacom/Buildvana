// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Whether <c>bv</c> manages a pin, and what stops it when it does not.
/// </summary>
/// <remarks>
/// <para>Everything but <see cref="Managed"/> names a decision the pin's author made. None of them is an
/// error: a repository adopting <c>bv dependencies</c> must not have to rewrite itself first, so an
/// unmanaged pin is listed, reported, and left exactly as it is.</para>
/// </remarks>
internal enum PinManagement
{
    /// <summary>The pin states one exact version, which is the only form an automatic update moves.</summary>
    Managed,

    /// <summary>
    /// The pin states one version in the bracket notation, e.g. <c>[13.0.4]</c>. The report suggests writing
    /// it without brackets, which is the same pin in a form <c>bv</c> manages.
    /// </summary>
    BracketExactVersion,

    /// <summary>
    /// The pin states a version range, e.g. <c>[1.0,2.0)</c>: whoever wrote it decided what may resolve.
    /// </summary>
    VersionRange,

    /// <summary>
    /// The pin states a floating version, e.g. <c>1.*</c>: restore picks the highest match every time it
    /// runs, so keeping the version current is already someone else's job.
    /// </summary>
    FloatingVersion,

    /// <summary>The pin states text NuGet reads as neither a version nor a range.</summary>
    UnreadableVersion,

    /// <summary>
    /// The item carries <c>VersionOverride</c> metadata: central package management's per-project exception,
    /// which states that this project departs from the central pin on purpose. The departure is per project
    /// and <c>bv</c>'s policies are per package id, so the two do not meet.
    /// </summary>
    VersionOverride,

    /// <summary>
    /// The declaring file does not state the version itself: an MSBuild property holds it, or a
    /// <c>PackageReference Update="..."</c> elsewhere applies it. The evaluated version is exact, but
    /// rewriting the file would break the indirection its author wanted. Writing the version as a literal is
    /// what makes such a pin managed.
    /// </summary>
    IndirectVersion,
}
