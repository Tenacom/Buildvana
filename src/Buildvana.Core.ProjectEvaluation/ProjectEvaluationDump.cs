// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Core.ProjectEvaluation;

/// <summary>
/// The evaluated facts of one project: what Buildvana SDK's project evaluation target writes, and what
/// <c>bv</c> reads to describe a solution's projects to hooks.
/// </summary>
/// <remarks>
/// <para>The members of this type come from the evaluation <c>dotnet pack</c> uses, which for a
/// multi-targeting project is the outer build. The facts of each target framework come from the inner build
/// of that framework, which is the only one that imports the build files of the project's packages.</para>
/// <para>The dump carries no format version. The SDK that writes it and the <c>bv</c> that reads it are
/// released in lockstep, and <c>bv</c> refuses to run against a repository pinning another version.</para>
/// </remarks>
public sealed record ProjectEvaluationDump
{
    /// <summary>Gets the full path of the evaluated project.</summary>
    public required string ProjectFullPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the project's <c>IsPackable</c> property evaluates to <c>true</c>.
    /// </summary>
    public bool IsPackable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the project's <c>PackAsTool</c> property evaluates to <c>true</c>.
    /// </summary>
    public bool PackAsTool { get; init; }

    /// <summary>
    /// Gets the evaluated value of the project's <c>PackageId</c> property, or <see langword="null"/> when
    /// it is empty.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets the facts of each target framework of the project, in the order the project states them.
    /// </summary>
    /// <remarks>
    /// <para>A project that states no target framework has no entry.</para>
    /// </remarks>
    public IReadOnlyList<ProjectEvaluationDumpTargetFramework> TargetFrameworks { get; init; } = [];
}
