// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// What a run of <c>bv dependencies</c> made of the pins of one additional package group.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record AdditionalPackagesResult
{
    /// <summary>Gets the group's caption, as configuration states it.</summary>
    public required string Caption { get; init; }

    /// <summary>Gets what the run made of the group's pins.</summary>
    public required IReadOnlyList<DependencyResult> Results { get; init; }
}
