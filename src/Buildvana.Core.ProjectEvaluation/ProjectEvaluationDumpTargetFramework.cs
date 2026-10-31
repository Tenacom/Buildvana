// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace Buildvana.Core.ProjectEvaluation;

/// <summary>
/// The evaluated facts of one target framework of a <see cref="ProjectEvaluationDump"/>.
/// </summary>
/// <remarks>
/// <para>The facts come from the evaluation of the project for this target framework. A package can set
/// <c>OutputType</c> or <c>IsTestProject</c> in its build files, and NuGet imports those files into the
/// evaluation of a target framework alone, so the outer build of a multi-targeting project does not see
/// them.</para>
/// </remarks>
public sealed record ProjectEvaluationDumpTargetFramework
{
    /// <summary>Gets the target framework, as the project states it.</summary>
    public required string TargetFramework { get; init; }

    /// <summary>Gets the evaluated value of the <c>TargetPath</c> property: the full path of the built assembly.</summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Gets the full path the build copies the XML documentation file to, next to <see cref="TargetPath"/>, or
    /// <see langword="null"/> when the evaluation generates no documentation file.
    /// </summary>
    /// <remarks>
    /// <para>The path is MSBuild's own <c>FinalDocFile</c> item, which the <c>CopyFilesToOutputDirectory</c>
    /// target copies the compiler's documentation file to.</para>
    /// </remarks>
    public string? DocumentationFile { get; init; }

    /// <summary>
    /// Gets the evaluated value of the <c>OutputType</c> property, or <see langword="null"/> when it is empty.
    /// </summary>
    public string? OutputType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <c>IsTestProject</c> property evaluates to <c>true</c>.
    /// </summary>
    public bool IsTestProject { get; init; }
}
