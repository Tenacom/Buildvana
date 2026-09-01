// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Diagnostics;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// What the package sources know about vulnerable packages, by package id.
/// </summary>
/// <remarks>
/// <para>A source states its whole vulnerability database at once, so the run reads it once and asks it
/// about one package at a time. The advisories of every source are merged: a package one source knows
/// nothing about may be covered by another's data.</para>
/// </remarks>
internal sealed class AdvisoryIndex
{
    private readonly Dictionary<string, IReadOnlyList<PackageAdvisory>> _advisories;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvisoryIndex"/> class.
    /// </summary>
    /// <param name="advisories">The advisories, each with the id of the package it covers, in any order and
    /// from any number of sources.</param>
    public AdvisoryIndex(IReadOnlyList<(string PackageId, PackageAdvisory Advisory)> advisories)
    {
        Guard.IsNotNull(advisories);
        _advisories = advisories
            .GroupBy(static entry => entry.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<PackageAdvisory>)[.. group.Select(static entry => entry.Advisory)],
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets an index that knows of no advisory at all.
    /// </summary>
    public static AdvisoryIndex Empty { get; } = new([]);

    /// <summary>
    /// Gets the advisories covering a package.
    /// </summary>
    /// <param name="packageId">The package id.</param>
    /// <returns>Every advisory known for the id, empty when none is.</returns>
    public IReadOnlyList<PackageAdvisory> For(string packageId)
    {
        Guard.IsNotNullOrWhiteSpace(packageId);
        return _advisories.TryGetValue(packageId, out var advisories) ? advisories : [];
    }
}
