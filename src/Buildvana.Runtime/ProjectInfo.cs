// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Describes one project of a <see cref="SolutionInfo"/>, as MSBuild evaluates it.
/// </summary>
/// <remarks>
/// <para>The members of this type come from the evaluation <c>dotnet pack</c> uses, which for a
/// multi-targeting project is the outer build.</para>
/// <para>A project that Buildvana SDK does not reach is not evaluated. Its entry states
/// <see cref="ProjectFile"/>, <see langword="null"/> for every evaluated fact, and no target framework.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ProjectInfo
{
    /// <summary>Gets the absolute path of the project file.</summary>
    public required string ProjectFile { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <c>IsPackable</c> property evaluates to <c>true</c>, or
    /// <see langword="null"/> when the project was not evaluated.
    /// </summary>
    public required bool? IsPackable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <c>PackAsTool</c> property evaluates to <c>true</c>, or
    /// <see langword="null"/> when the project was not evaluated.
    /// </summary>
    public required bool? PackAsTool { get; init; }

    /// <summary>
    /// Gets the evaluated value of the <c>PackageId</c> property, or <see langword="null"/> when it is empty or
    /// the project was not evaluated.
    /// </summary>
    public required string? PackageId { get; init; }

    /// <summary>
    /// Gets one entry per target framework of the project, in the order the project states them.
    /// </summary>
    /// <remarks>
    /// <para>The list is empty when the project was not evaluated, or states no target framework.</para>
    /// </remarks>
    public required IReadOnlyList<TargetFrameworkInfo> TargetFrameworks { get; init; }
}
