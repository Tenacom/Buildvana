// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Core.Dependencies;

/// <summary>
/// The package items one evaluation of one project declares: what Buildvana SDK's pin dump target writes,
/// and what <c>bv</c> reads to see a solution's package pins.
/// </summary>
/// <remarks>
/// <para>A multi-targeting project is evaluated once per target framework, so it produces one dump per
/// framework. An item conditioned on the target framework therefore appears in the dumps of the frameworks
/// whose evaluation declares it, and nowhere else.</para>
/// <para>The dump carries no format version. The SDK that writes it and the <c>bv</c> that reads it are
/// released in lockstep, and <c>bv</c> refuses to run against a repository pinning another version.</para>
/// </remarks>
public sealed record PackagePinDump
{
    /// <summary>Gets the full path of the evaluated project.</summary>
    public required string ProjectFullPath { get; init; }

    /// <summary>
    /// Gets the target framework the project was evaluated for, or <see langword="null"/> for a project that
    /// has none.
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets a value indicating whether the project manages its package versions centrally, which is the
    /// evaluated value of its <c>ManagePackageVersionsCentrally</c> property.
    /// </summary>
    public bool ManagePackageVersionsCentrally { get; init; }

    /// <summary>Gets the package items the evaluation declares, in evaluation order.</summary>
    public IReadOnlyList<PackagePinDumpItem> Items { get; init; } = [];
}
