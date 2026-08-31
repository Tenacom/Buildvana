// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What an invocation asks of resolution beyond the pins themselves: which pins it is about, and whether it
/// states the version they must reach.
/// </summary>
/// <remarks>
/// <para>A filter names package ids, as a glob or as an id of its own. A pin no filter names is skipped
/// rather than left out, so that a hook deriving state from a particular pin sees it in every run.</para>
/// <para>A stated version is an assisted manual edit. It overrules the policy, downgrades included, which is
/// the one thing no automatic update ever does.</para>
/// </remarks>
internal sealed record DependencyResolutionRequest
{
    /// <summary>Gets the request of a run that asks for nothing beyond the policies.</summary>
    public static DependencyResolutionRequest None { get; } = new();

    /// <summary>Gets the patterns naming the pins the invocation is about; empty when it is about all of them.</summary>
    public IReadOnlyList<string> Filters { get; init; } = [];

    /// <summary>Gets the version the invocation states, or <see langword="null"/> when it states none.</summary>
    public NuGetVersion? To { get; init; }

    /// <summary>
    /// Gets the package id whose pins the invocation sets to <see cref="To"/>, or <see langword="null"/> when
    /// the invocation states no version, or states one for the .NET SDK baseline.
    /// </summary>
    public string? TargetId => To is not null && Filters.Count == 1 ? Filters[0] : null;

    /// <summary>
    /// Tells whether the invocation is about a package id.
    /// </summary>
    /// <param name="id">The package id.</param>
    /// <returns><see langword="true"/> if a filter names the id, or the invocation has no filter.</returns>
    public bool Names(string id) => Filters.Count == 0 || Filters.Any(filter => PackageIdPattern.Matches(filter, id));
}
