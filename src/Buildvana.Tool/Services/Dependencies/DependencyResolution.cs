// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What a run made of everything the repository pins in the selected scopes: the offline inventory, answered
/// against the package sources and the .NET release index.
/// </summary>
/// <remarks>
/// <para>A scope that was not selected contributes nothing here, which is not the same as a scope that has
/// no pin.</para>
/// </remarks>
internal sealed record DependencyResolution
{
    /// <summary>
    /// Gets what the run made of the .NET SDK baseline, or <see langword="null"/> when the scope was not
    /// selected or the repository pins none.
    /// </summary>
    public NetSdkResolution? NetSdk { get; init; }

    /// <summary>Gets what the run made of the MSBuild project SDK pins.</summary>
    public IReadOnlyList<PinResolution> Sdks { get; init; } = [];

    /// <summary>Gets what the run made of the .NET local tool pins.</summary>
    public IReadOnlyList<PinResolution> Tools { get; init; } = [];

    /// <summary>Gets what the run made of the package pins, additional groups included.</summary>
    public IReadOnlyList<PinResolution> Packages { get; init; } = [];
}
