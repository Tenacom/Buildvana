// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Buildvana.Tool.Services.Dependencies;
using NuGet.Versioning;

/// <summary>
/// A package version source scripted with what each id's sources know, recording what was asked of it.
/// </summary>
internal sealed class FakePackageVersionSource : IPackageVersionSource
{
    private readonly Dictionary<string, PackageVersionCatalog> _catalogs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the names of the configured sources.</summary>
    public IReadOnlyList<string> Sources { get; set; } = ["local"];

    /// <summary>Gets the ids that were asked about, in order.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// Scripts what the sources know about an id.
    /// </summary>
    /// <param name="id">The package id.</param>
    /// <param name="listed">The versions the sources list.</param>
    /// <param name="unlisted">The versions the sources know and have delisted.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakePackageVersionSource Knows(string id, string[] listed, string[]? unlisted = null)
    {
        _catalogs[id] = new PackageVersionCatalog
        {
            Listed = [.. listed.Select(NuGetVersion.Parse)],
            Unlisted = [.. (unlisted ?? []).Select(NuGetVersion.Parse)],
        };

        return this;
    }

    /// <inheritdoc/>
    public Task<PackageVersionCatalog> GetVersionsAsync(string packageId, CancellationToken cancellationToken = default)
    {
        Asked.Add(packageId);
        return Task.FromResult(_catalogs.TryGetValue(packageId, out var catalog) ? catalog : PackageVersionCatalog.Empty);
    }
}
