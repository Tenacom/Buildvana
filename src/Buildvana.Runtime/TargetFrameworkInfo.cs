// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// Describes one target framework of a <see cref="ProjectInfo"/>, as MSBuild evaluates the project for it.
/// </summary>
/// <remarks>
/// <para>A package can set <c>OutputType</c> or <c>IsTestProject</c> in its build files. NuGet imports those
/// files into the evaluation for a target framework alone, so these facts are stated per target
/// framework.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record TargetFrameworkInfo
{
    /// <summary>Gets the target framework, as the project states it, such as <c>net10.0</c>.</summary>
    public required string TargetFramework { get; init; }

    /// <summary>
    /// Gets the evaluated value of the <c>TargetPath</c> property: the absolute path of the built assembly.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Gets the absolute path of the XML documentation file next to <see cref="TargetPath"/>, or
    /// <see langword="null"/> when the project generates none.
    /// </summary>
    public required string? DocumentationFile { get; init; }

    /// <summary>
    /// Gets the evaluated value of the <c>OutputType</c> property, or <see langword="null"/> when it is empty.
    /// </summary>
    public required string? OutputType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <c>IsTestProject</c> property evaluates to <c>true</c>.
    /// </summary>
    public required bool IsTestProject { get; init; }
}
