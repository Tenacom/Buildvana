// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Everything a repository pins, as its own files state it: the offline half of what
/// <c>bv dependencies</c> works on.
/// </summary>
/// <remarks>
/// <para>Only the selected scopes contribute. A scope that was not selected has no pins here, which is not
/// the same as a scope that has none.</para>
/// </remarks>
internal sealed record DependencyInventory
{
    /// <summary>
    /// Gets the .NET SDK baseline, or <see langword="null"/> when the scope was not selected or the
    /// repository pins none.
    /// </summary>
    public NetSdkPin? NetSdk { get; init; }

    /// <summary>Gets the MSBuild project SDK pins.</summary>
    public IReadOnlyList<DependencyPin> Sdks { get; init; } = [];

    /// <summary>Gets the .NET local tool pins.</summary>
    public IReadOnlyList<DependencyPin> Tools { get; init; } = [];

    /// <summary>
    /// Gets the package pins: the ones MSBuild evaluated, the ones an additional group declares, and the
    /// ones a file-based app states in a directive. A pin of an additional group carries its caption.
    /// </summary>
    public IReadOnlyList<DependencyPin> Packages { get; init; } = [];
}
