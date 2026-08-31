// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Buildvana.Runtime;

/// <summary>
/// What a run of <c>bv dependencies</c> made of one pin, as the <c>deps/post-update</c> hook is told it.
/// </summary>
/// <remarks>
/// <para>Versions travel as strings: this package's dependency closure is the base class library, which has
/// no version type NuGet's rules apply to.</para>
/// </remarks>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DependencyResult
{
    /// <summary>
    /// Gets the package id, or <see langword="null"/> for the .NET SDK baseline, which has none.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets the path of the file that declares the pin, relative to the home directory, with forward slashes.
    /// </summary>
    public required string DeclaringFile { get; init; }

    /// <summary>Gets the pin as it stood before the invocation.</summary>
    public required string CurrentVersion { get; init; }

    /// <summary>
    /// Gets the version the pin reached, or would reach in a check run, or <see langword="null"/> when there
    /// is none.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>Gets what the run made of the pin.</summary>
    public required DependencyResultState State { get; init; }

    /// <summary>
    /// Gets the highest stable version the sources have, or <see langword="null"/> when nothing was resolved.
    /// </summary>
    public string? LatestStable { get; init; }

    /// <summary>
    /// Gets the highest prerelease version the sources have, or <see langword="null"/> when nothing was
    /// resolved.
    /// </summary>
    public string? LatestPreview { get; init; }

    /// <summary>Gets the policy that governs the pin, in policy-string syntax.</summary>
    public required string Policy { get; init; }
}
