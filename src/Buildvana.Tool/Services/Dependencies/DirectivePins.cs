// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What the repository's file-based apps state in their leading directive blocks.
/// </summary>
/// <param name="Pins">The pins the directives state, in walk order.</param>
/// <param name="References">The package ids that versionless <c>#:package</c> directives name, in walk order
/// and without repetition.</param>
/// <remarks>
/// <para>A directive carrying a version is a pin, and a versionless one is a reference to a pin declared
/// elsewhere. Both are read in one walk, because both concern the same files and neither is worth a second
/// pass over the repository.</para>
/// </remarks>
internal sealed record DirectivePins(IReadOnlyList<DependencyPin> Pins, IReadOnlyList<string> References)
{
    /// <summary>Gets what a walk that never ran answers with.</summary>
    public static DirectivePins None { get; } = new([], []);
}
