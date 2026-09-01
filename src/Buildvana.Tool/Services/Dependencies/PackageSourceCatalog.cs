// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Buildvana.Core;
using Buildvana.Core.HomeDirectory;
using CommunityToolkit.Diagnostics;
using NuGet.Configuration;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Reads the package sources a restore of the repository would use, and says which of them answer for a
/// given package id.
/// </summary>
/// <remarks>
/// <para>The sources come from NuGet's own hierarchical configuration chain, read from the home directory
/// upwards through the user and machine levels. Restore reads the same chain through the same library, so
/// what <c>bv dependencies</c> sees is what a restore sees, source mapping included.</para>
/// <para>Reading is deferred to the first question asked: a command that resolves nothing, such as
/// <c>bv dependencies show</c>, never touches a <c>nuget.config</c>.</para>
/// </remarks>
internal sealed class PackageSourceCatalog
{
    private readonly Lazy<IReadOnlyList<PackageSource>> _sources;
    private readonly Lazy<PackageSourceMapping> _mapping;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageSourceCatalog"/> class.
    /// </summary>
    /// <param name="home">The home directory provider, naming the directory the configuration chain starts
    /// at.</param>
    public PackageSourceCatalog(IHomeDirectoryProvider home)
    {
        Guard.IsNotNull(home);
        var settings = new Lazy<ISettings>(() => LoadSettings(home));
        _sources = new Lazy<IReadOnlyList<PackageSource>>(() => [.. SettingsUtility.GetEnabledSources(settings.Value)]);
        _mapping = new Lazy<PackageSourceMapping>(() => PackageSourceMapping.GetPackageSourceMapping(settings.Value));
    }

    /// <summary>
    /// Gets the enabled package sources, in configuration order.
    /// </summary>
    /// <exception cref="BuildFailedException">The configuration chain could not be read.</exception>
    public IReadOnlyList<PackageSource> Sources => _sources.Value;

    /// <summary>
    /// Gets the sources that answer for a package id.
    /// </summary>
    /// <param name="packageId">The package id.</param>
    /// <returns>The sources to ask about the id: every enabled source, or the ones package source mapping
    /// maps the id to. The result is empty when mapping maps the id to no enabled source, which is also what
    /// a restore would make of it.</returns>
    /// <exception cref="BuildFailedException">The configuration chain could not be read.</exception>
    public IReadOnlyList<PackageSource> SourcesFor(string packageId)
    {
        Guard.IsNotNullOrWhiteSpace(packageId);
        if (!_mapping.Value.IsEnabled)
        {
            return Sources;
        }

        var names = _mapping.Value.GetConfiguredPackageSources(packageId);
        if (names.Count == 0)
        {
            return [];
        }

        var mapped = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return [.. Sources.Where(source => mapped.Contains(source.Name))];
    }

    private static ISettings LoadSettings(IHomeDirectoryProvider home)
    {
        try
        {
            return Settings.LoadDefaultSettings(home.HomeDirectory);
        }
        catch (NuGetConfigurationException exception)
        {
            throw new BuildFailedException($"The NuGet configuration could not be read: {exception.Message}", exception);
        }
    }
}
