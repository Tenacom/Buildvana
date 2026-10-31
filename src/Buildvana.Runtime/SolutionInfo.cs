// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Describes the projects of the solution in the home directory, as MSBuild evaluates them in the build
/// configuration of the command that runs the hook. Shared by every hook's args.
/// </summary>
/// <remarks>
/// <para>The model states MSBuild properties, and derives no project type from them. A project can be several
/// things at once: a test project on Microsoft.Testing.Platform has <c>OutputType</c> <c>Exe</c>, for example.
/// The hook decides what a combination of properties means to it.</para>
/// <para>The paths state where a build in <see cref="Configuration"/> puts its files. The files exist only when
/// the command has built them, as <c>bv release</c> has when its hooks run.</para>
/// <para>The model is a snapshot, taken before the first hook of the command runs. A hook that changes a
/// project file does not change the model the next hook gets.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SolutionInfo
{
    /// <summary>
    /// Gets the absolute path of the solution file, or <see langword="null"/> when the home directory has none.
    /// </summary>
    public required string? SolutionFile { get; init; }

    /// <summary>
    /// Gets the build configuration the projects were evaluated in, such as <c>Release</c>.
    /// </summary>
    public required string Configuration { get; init; }

    /// <summary>
    /// Gets one entry per project of the solution file, in the order the solution file states them.
    /// </summary>
    /// <remarks>
    /// <para>The list is empty when the home directory has no solution file.</para>
    /// </remarks>
    public required IReadOnlyList<ProjectInfo> Projects { get; init; }
}
