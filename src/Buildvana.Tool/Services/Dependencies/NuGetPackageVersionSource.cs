// Copyright (C) Tenacom and Contributors. Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildvana.Core;
using Buildvana.Core.ConsoleOutput;
using Buildvana.Core.Diagnostics;
using CommunityToolkit.Diagnostics;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Buildvana.Tool.Services.Dependencies;

/// <summary>
/// Asks the repository's package sources what they know about a package id, through the same client
/// libraries a restore uses.
/// </summary>
/// <remarks>
/// <para>An id is looked up once per run and the answer kept: the same package is commonly pinned in several
/// files, and each of those pins asks the same question.</para>
/// <para>A source that cannot answer stops the run. A resolution against the sources that happened to reply
/// could only be wrong in silence, and reporting a pin as up to date is a claim, not a guess.</para>
/// </remarks>
internal sealed class NuGetPackageVersionSource : IPackageVersionSource, IDisposable
{
    private readonly PackageSourceCatalog _catalog;
    private readonly NuGetReporterLogger _logger;
    private readonly SourceCacheContext _cacheContext;
    private readonly Dictionary<string, PackageVersionCatalog> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SourceRepository> _repositories = new(StringComparer.OrdinalIgnoreCase);
    private bool _credentialsReady;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPackageVersionSource"/> class.
    /// </summary>
    /// <param name="catalog">The sources to ask.</param>
    /// <param name="reporter">The reporter carrying what the client libraries have to say.</param>
    public NuGetPackageVersionSource(PackageSourceCatalog catalog, IReporter reporter)
    {
        Guard.IsNotNull(catalog);
        Guard.IsNotNull(reporter);
        _catalog = catalog;
        _logger = new NuGetReporterLogger(reporter);

        // Versions are looked up once per run, so the round trip is worth its cost: an answer served from
        // the HTTP cache could report a pin as up to date hours after it stopped being so.
        _cacheContext = new SourceCacheContext { NoCache = true };
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Sources => [.. _catalog.Sources.Select(static source => source.Name)];

    /// <inheritdoc/>
    public async Task<PackageVersionCatalog> GetVersionsAsync(string packageId, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNullOrWhiteSpace(packageId);
        if (_known.TryGetValue(packageId, out var known))
        {
            return known;
        }

        var listed = new HashSet<NuGetVersion>(VersionComparer.VersionRelease);
        var unlisted = new HashSet<NuGetVersion>(VersionComparer.VersionRelease);
        foreach (var source in _catalog.SourcesFor(packageId))
        {
            var metadata = await ReadMetadataAsync(source, packageId, cancellationToken).ConfigureAwait(false);
            foreach (var package in metadata)
            {
                _ = (package.IsListed ? listed : unlisted).Add(package.Identity.Version);
            }
        }

        // A version one source lists and another has delisted is a version a restore would take.
        unlisted.ExceptWith(listed);
        var catalog = new PackageVersionCatalog { Listed = Ordered(listed), Unlisted = Ordered(unlisted) };
        _known[packageId] = catalog;
        return catalog;
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _cacheContext.Dispose();

    private static IReadOnlyList<NuGetVersion> Ordered(HashSet<NuGetVersion> versions)
        => [.. versions.OrderBy(static version => version, VersionComparer.VersionRelease)];

    private async Task<IEnumerable<IPackageSearchMetadata>> ReadMetadataAsync(
        PackageSource source,
        string packageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var repository = GetRepository(source);
            var resource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false);
            BuildFailedException.ThrowIf(
                resource is null,
                $"Package source '{source.Name}' cannot be asked what versions of a package it has.");
            return await resource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: true,
                _cacheContext,
                _logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is NuGetProtocolException || exception.IsIORelatedException)
        {
            // A source that cannot be reached fails the same way whether the client libraries name the
            // failure or the environment raises it: an unreadable cache directory is code 1, not a stack trace.
            throw new BuildFailedException(
                $"Package source '{source.Name}' could not be asked about {packageId}: {exception.Message}",
                exception);
        }
    }

    private SourceRepository GetRepository(PackageSource source)
    {
        // An authenticated source is reached with whatever credential providers the machine has, and never
        // by asking: a command that stopped for a prompt would hang a CI run instead of failing it. The
        // plumbing is process-wide state, so it is set up when a source that could need it first turns up.
        if (source.IsHttp && !_credentialsReady)
        {
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(_logger, nonInteractive: true);
            _credentialsReady = true;
        }

        if (!_repositories.TryGetValue(source.Name, out var repository))
        {
            repository = Repository.Factory.GetCoreV3(source);
            _repositories[source.Name] = repository;
        }

        return repository;
    }
}
